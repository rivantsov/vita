using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Reflection;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Entities.Runtime;
using System.Collections;

namespace Vita.Data.Driver {

  //Builds DML (Select/Insert/Update/Delete) SQL commands and stored procedures
  public class DbSqlBuilder {
    public const string ErrorTagConcurrentUpdateConflict = "ConcurrencyConflict";
    protected SqlSourceHasher SourceHasher = new SqlSourceHasher(); 

    protected DbModel DbModel;
    protected DbModelConfig ModelConfig { get { return DbModel.Config; } }

    public DbSqlBuilder(DbModel dbModel) { 
      DbModel = dbModel; 
    }

    public virtual string GetFullName(string schema, string name) {
      return ModelConfig.Driver.GetFullName(schema, name); 
    }

    // Builds a SELECT command without paging. For now paging is provider-dependent.
    public virtual DbCommandInfo BuildSelectAllCommand(EntityCommand entityCommand) {
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      const string SqlSelectAllTemplate = 
@"SELECT {0} 
FROM {1}{2};"; //note: no space between 1 & 2
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      var strOrderBy = BuildOrderBy(table.DefaultOrderBy);
      //Pg SQL is picky about extra spaces - it removes space before ';' when saving stored proc, so let's be careful here
      if(!string.IsNullOrEmpty(strOrderBy))
        strOrderBy = " " + strOrderBy;
      var sql = string.Format(SqlSelectAllTemplate, strColumns, table.FullName, strOrderBy);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectAll");
      
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, sql);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;
    }

    public virtual EntityMaterializer CreateEntityMaterializer(DbTableInfo table, IList<DbColumnInfo> columns) {
      var matzr = new EntityMaterializer(table);
      foreach (var col in columns)
        matzr.AddColumn(col);
      return matzr; 
    }

    // Paging is provider-specific.  Drivers should overwrite this method to implement paging.
    public virtual DbCommandInfo BuildSelectAllPagedCommand(EntityCommand entityCommand) {
      return null; 
    }

    public virtual DbCommandInfo BuildSqlDeleteCommand(EntityCommand entityCommand) {
      const string SqlDeleteTemplate = "DELETE FROM {0} WHERE {1};";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      //Load by primary key
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "Delete");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      var strWhere = BuildWhereClause(cmdInfo.Parameters);
      cmdInfo.Sql = string.Format(SqlDeleteTemplate, table.FullName, strWhere);
      return cmdInfo; 
    }

    public virtual DbCommandInfo BuildSqlUpdateCommand(EntityCommand entityCommand) {
      const string SqlUpdateTemplate = 
@"UPDATE {0} 
  SET {1} 
WHERE {2};";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      if (entityCommand == null)
        return null; 
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "Update");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      var pkParams = cmdInfo.Parameters.Where(p => p.SourceColumn.Flags.IsSet(DbColumnFlags.PrimaryKey));
      // find members to update. For partial update, with explicitly specified members to update, we ignore NoUpdate flag
      var excludeFlags = DbColumnFlags.PrimaryKey;
      if (entityCommand.Kind == EntityCommandKind.Update)
        excludeFlags |= DbColumnFlags.NoUpdate;
      var updateParams = cmdInfo.Parameters.Where(p => !p.SourceColumn.Flags.IsSet(excludeFlags));
      // Some tables (like many-to-many link entities) might have no columns to update
      if (!updateParams.Any())
        return null;
      //Build Where expression
      var whereExpr = BuildWhereClause(pkParams.ToList());
      //Build Update clause
      var qt = "\"";
      var updList = new StringList();
      foreach (var prm in updateParams) 
        updList.Add(qt + prm.SourceColumn.ColumnName + qt + " = " + prm.Name);
      var strUpdates = string.Join(", ", updList);
      //Build SQL
      cmdInfo.Sql = string.Format(SqlUpdateTemplate, table.FullName, strUpdates, whereExpr);
      return cmdInfo; 
    }

    public virtual DbCommandInfo BuildSqlInsertCommand(EntityCommand entityCommand) {
      const string SqlInsertTemplate = "INSERT INTO {0} \r\n  ({1}) \r\n  VALUES \r\n    ({2});";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var idClause = string.Empty; 
      var listColumns = new List<DbColumnInfo>(); 
      var listValues = new StringList();
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "Insert");
      var dbCmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      foreach (var prm in dbCmdInfo.Parameters) {
        var col = prm.SourceColumn;
        if (!col.Flags.IsSet(DbColumnFlags.NoInsert)) {
          listColumns.Add(col);
          listValues.Add(prm.Name);
        }
      }
      //build SQL
      var strColumns = listColumns.GetSqlNameList();
      var strValues = string.Join(", ", listValues);
      dbCmdInfo.Sql = string.Format(SqlInsertTemplate, table.FullName, strColumns, strValues) + idClause;
      return dbCmdInfo; 
    }

    public virtual DbCommandInfo BuildSelectByKeyCommand(EntityCommand entityCommand) {
      const string SqlSelectByFkTemplate = @"
SELECT {0} {1} 
  FROM {2} 
  WHERE {3} 
  {4}";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var dbKey = DbModel.LookupDbObject<DbKeyInfo>(entityCommand.SelectKey); 
      var keyCols = dbKey.KeyColumns.GetNames(removeUnderscores: true);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectBy", keyCols);
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, null);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      //build WHERE clause
      var whereExpr = BuildWhereClause(cmdInfo.Parameters);
      if (!string.IsNullOrWhiteSpace(entityCommand.Filter))
        whereExpr = whereExpr + " AND " + ProcessFilter(entityCommand, table);
      string orderByExpr = null;
      if (dbKey.KeyType == KeyType.PrimaryKey)
        orderByExpr = null; 
      else 
        orderByExpr = BuildOrderBy(table.DefaultOrderBy);
      string strTop = string.Empty;
      var sql = string.Format(SqlSelectByFkTemplate, strTop, strColumns, table.FullName, whereExpr, orderByExpr);
      //Damn postgres reformats the SQL in stored proc body and this screws up comparison; so we are careful here
      sql = sql.Trim() + ";";
      cmdInfo.Sql = sql;
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;
    }

    protected virtual string ProcessFilter(EntityCommand command, DbTableInfo table) {
      var filter = command.Filter;
      if (filter == null || !filter.Contains('{'))
        return filter;
      foreach (var col in table.Columns) {
        var name = "{" + col.Member.MemberName + "}";
        if (!filter.Contains(name))
          continue;
        var colRef = '"' + col.ColumnName + '"';
        filter = filter.Replace(name, colRef);
      }
      filter = "(" + filter + ")";
      return filter;
    }

    //Creates default implementation for servers that do not support array parameters.
    // The command is implemented as templated SQL
    public virtual DbCommandInfo BuildSelectByKeyArrayCommand(EntityCommand entityCommand) {
      const string SqlSelectByFkTemplate = @"
SELECT {0} {1} 
  FROM {2} 
  WHERE {3} 
  {4}";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var dbKey = DbModel.LookupDbObject<DbKeyInfo>(entityCommand.SelectKey);
      if (dbKey.KeyColumns.Count > 1)
        return null; 
      var keyCol = dbKey.KeyColumns[0].Column.ColumnName;
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectByArrayOf_", keyCol);
      var descrTag = GetDbCommandDescriptiveTag(entityCommand);
      var cmdInfo = new DbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, null, descrTag);
      cmdInfo.IsTemplatedSql = true;
      
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      //build WHERE clause

      var whereExpr = '"' + keyCol + '"' + " IN ({0})"; //this {0} will remain in a template
      if (!string.IsNullOrWhiteSpace(entityCommand.Filter))
        whereExpr = whereExpr + " AND " + ProcessFilter(entityCommand, table);
      string orderByExpr = null;
      if (dbKey.KeyType == KeyType.PrimaryKey)
        orderByExpr = null;
      else
        orderByExpr = BuildOrderBy(table.DefaultOrderBy);
      string strTop = string.Empty;
      var sql = string.Format(SqlSelectByFkTemplate, strTop, strColumns, table.FullName, whereExpr, orderByExpr);
      sql = sql.Trim() + ";";
      cmdInfo.Sql = sql;
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      //Create parameter for just-in-time formatting of SQL
      var entPrm = entityCommand.Parameters[0];
      Type elemType; 
      Util.Check(entPrm.DataType.IsListOfDbPrimitive(out elemType), "Parameter is not list of primitives.");
      var elemTypeInfo = GetDbTypeInfo(elemType, 0);
      Util.Check(elemTypeInfo != null, "Failed to get db type information for type {0}.", elemType);
      var prm = new DbParamInfo(entPrm, "(array)", 0);
      prm.ToLiteralConverter = (list) => ConvertList(list, elemTypeInfo);
      cmdInfo.Parameters.Add(prm);
      return cmdInfo;
    }

    private static string ConvertList(object value, DbTypeInfo elemTypeInfo) {
      var strings = new List<string>();
      var iEnum = value as IEnumerable;
      foreach (var v in iEnum) {
        strings.Add(elemTypeInfo.ToLiteral(elemTypeInfo.PropertyToColumnConverter(v)));
      }
      return string.Join(", ", strings);
    }

    public virtual DbCommandInfo BuildSelectManyToManyCommand(EntityCommand entityCommand) {
      const string SqlTemplate = @"
SELECT {0} 
  FROM {1} L INNER JOIN {2} T ON {3}  
  WHERE {4}
  {5};";
      var parentRefKey = entityCommand.SelectKey;
      var targetRefKey = entityCommand.SelectKeySecondary;
      var targetTable = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo);
      var dbParentRefKey = DbModel.LookupDbObject<DbKeyInfo>(parentRefKey);
      var dbTargetRefKey = DbModel.LookupDbObject<DbKeyInfo>(targetRefKey);
      var linkTable = dbParentRefKey.Table;
      var parentRefColNames = dbParentRefKey.KeyColumns.GetNames(removeUnderscores: true);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, targetTable.TableName, "SelectBy", parentRefColNames);
      // var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, targetTable, DbExecutionType.Reader, null);
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, linkTable, DbExecutionType.Reader, null);
      var outColumns = targetTable.Columns.GetSelectable();
      var strOutColumns = outColumns.GetSqlNameList("T");
      var whereExpr = BuildWhereClause(cmdInfo.Parameters, "L");
      var orderByExpr = BuildOrderBy(linkTable.DefaultOrderBy, "L");
      string joinOnClause = BuildJoinClause(dbTargetRefKey, targetTable.PrimaryKey, "L", "T");
      cmdInfo.Sql = string.Format(SqlTemplate, strOutColumns, linkTable.FullName, targetTable.FullName, joinOnClause, whereExpr, orderByExpr);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(targetTable, outColumns);
      return cmdInfo;
    }

    protected virtual string BuildOrderBy(IList<DbKeyColumnInfo> orderByEntries, string tableAlias = null) {
      if(orderByEntries == null || orderByEntries.Count == 0)
        return string.Empty; 
      return "ORDER BY " + orderByEntries.GetSqlNameListWithOrderSpec(tableAlias);
    }

    protected virtual string BuildOrderByColumns(IList<DbKeyColumnInfo> orderByEntries, string tableAlias = null) {
      if (orderByEntries == null || orderByEntries.Count == 0)
        return null;
      var strEntries = orderByEntries.Select(e => FormatOrderByEntry(e));
      if (string.IsNullOrEmpty(tableAlias))
        return string.Join(", ", strEntries);
      var delim = ", " + tableAlias + ".";
      return tableAlias + "." + string.Join(delim, strEntries);
    }

    protected virtual string FormatOrderByEntry(DbKeyColumnInfo colInfo) {
      return colInfo.ToString();
    }

    protected virtual string BuildWhereClause(List<DbParamInfo> keyParameters, string tableAlias = null) {
      string colPrefix = string.IsNullOrEmpty(tableAlias) ? string.Empty : tableAlias + ".";
      var whereExprs = new StringList();
      foreach (var prm in keyParameters)
        whereExprs.Add(colPrefix + "\"" + prm.SourceColumn.ColumnName + "\"" + " = " + prm.Name);
      return string.Join(" AND ", whereExprs);
    }

    protected virtual string BuildJoinClause(DbKeyInfo key1, DbKeyInfo key2, string table1Alias, string table2Alias) {
      var prefix1 = string.IsNullOrEmpty(table1Alias) ? string.Empty : table1Alias + ".";
      var prefix2 = string.IsNullOrEmpty(table2Alias) ? string.Empty : table2Alias + ".";
      var count = key1.KeyColumns.Count;
      var parts = new string[count];
      for (int i = 0; i < count; i++)
        parts[i] = string.Format("{0}\"{1}\" = {2}\"{3}\"", prefix1, key1.KeyColumns[i].Column.ColumnName, 
                                                            prefix2, key2.KeyColumns[i].Column.ColumnName);
      return string.Join(" AND ", parts);
    }

    public virtual string GetParameterPrefix() {
      var driver = this.ModelConfig.Driver;
      var prefix = this.ModelConfig.Options.IsSet(DbOptions.UseStoredProcs) ? driver.StoredProcParameterPrefix : driver.DynamicSqlParameterPrefix;
      return prefix; 
    }

    public virtual DbCommandInfo BuildSqlSequenceGetNextCommand(DbSequenceInfo sequence) {
      return null;
    }

    // Stored procedures ==============================================================
    public virtual bool ConvertToStoredProc(DbCommandInfo command) {
      Util.Throw("Stored procedures are not supported by driver {0}", this.GetType().Name);
      return false;
    }

    public virtual DbCommandInfo CreateDbCommandInfo(EntityCommand entityCommand, string name, DbTableInfo mainTable, DbExecutionType executionType, string sql) {
      var descrTag = GetDbCommandDescriptiveTag(entityCommand); 
      var cmdInfo = new DbCommandInfo(entityCommand, name, mainTable, executionType, sql, descrTag);
      //Create parameters from entity command parameters 
      var policy = DbModel.Config.NamingPolicy;
      var prmPrefix = GetParameterPrefix();
      for(int i=0; i< entityCommand.Parameters.Count; i++) {
        var entParam = entityCommand.Parameters[i];
        var paramName = prmPrefix + policy.ConstructDbParameterName(entParam.Name);
        DbParamInfo prmInfo;
        if (entParam.SourceMember != null) {
          var col = mainTable.Columns.FirstOrDefault(c => c.Member == entParam.SourceMember);
          Util.Check(col != null, "Failed to find Db column for member {0}, entity {1}.", entParam.SourceMember.MemberName, mainTable.Entity.Name);
          prmInfo = cmdInfo.AddParameter(entParam, paramName, col, i);
        } else {
          var typeInfo = GetDbTypeInfo(entParam.DataType, entParam.Size);
          prmInfo = cmdInfo.AddParameter(entParam, paramName, typeInfo, i);
        }
        // SQL CE does not support output parameters
        if (!this.DbModel.Driver.Supports(DbFeatures.OutputParameters))
          prmInfo.Direction = ParameterDirection.Input;
        if (prmInfo.Direction == ParameterDirection.Output || prmInfo.Direction == ParameterDirection.InputOutput) {
          cmdInfo.PostUpdateActions.Add((con, cmd, rec) => {
            var prm = (IDbDataParameter) cmd.Parameters[prmInfo.Name];
            rec.SetValueDirect(prmInfo.SourceColumn.Member, prm.Value);
          });
        }
      }//foreach entParam
      return cmdInfo;
    }

    //Overridden in MsSql and Postgres to handle array parameters
    public virtual DbTypeInfo GetDbTypeInfo(Type clrType, int size) {
      return this.DbModel.Driver.TypeRegistry.GetDbTypeInfo(clrType, size);
    }

    //Produces Descriptive tag that identifies the proc and its purpose. It is used to map proc in the database to CRUD entity command
    public string GetDbCommandDescriptiveTag(EntityCommand command) {
      var table =  DbModel.GetTable(command.TargetEntityType, throwIfNotFound: false);
      Util.Check(table != null, "Target Db table not found for entity command {0}", command);
      var tag = "CRUD/" + table.FullName + "/" + command.Kind;
      switch(command.Kind) {
        case EntityCommandKind.SelectAll:
        case EntityCommandKind.SelectAllPaged:
          break;
        case EntityCommandKind.SelectByKey:
        case EntityCommandKind.SelectByKeyArray:
        case EntityCommandKind.SelectByKeyManyToMany:
          var dbKey = DbModel.LookupDbObject<DbKeyInfo>(command.SelectKey);
          tag += "/" + string.Join(",", dbKey.KeyColumns);
          break;
        case EntityCommandKind.CustomSelect:
        case EntityCommandKind.CustomInsert:
        case EntityCommandKind.CustomUpdate:
        case EntityCommandKind.CustomDelete:
          tag += "/" + command.CommandName;
          break;
        case EntityCommandKind.PartialUpdate:
          tag += "/" + string.Join(string.Empty, command.UpdateMemberNames);
          break;
      }
      if (!string.IsNullOrWhiteSpace(command.Filter))
        tag += "/Filter:" + command.Filter; 
      return tag;
    }

    protected virtual string GetGrantStatement(DbCommandInfo command) {
      return null; 
    }

    protected class SqlLimitClause {
      public string TopClause; //Top clause, used by SQL Server 
      public string LimitClause; //Limit/Fetch clause, added at the end; used by MySql
    }
    protected virtual SqlLimitClause GetSqlLimitClause(string count) {
      return new SqlLimitClause() { TopClause = "TOP " + count };
    }


  }//class
}//namespace
