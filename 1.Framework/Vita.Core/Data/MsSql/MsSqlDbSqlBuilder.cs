using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using System.Collections;

namespace Vita.Data.MsSql {
  
  public partial class MsSqlDbSqlBuilder : DbSqlBuilder {
    MsSqlVersion _version;

    public MsSqlDbSqlBuilder(DbModel dbModel, MsSqlVersion version) : base(dbModel) {
      _version = version;
    }

    public override DbCommandInfo BuildSelectAllPagedCommand(EntityCommand entCommand) {
      switch(_version) {
        case MsSqlVersion.V2012:
          return BuildSelectAllPagedCommand2012(entCommand);
        case MsSqlVersion.V2008:
        default:
          return BuildSelectAllPagedCommand2008(entCommand);
      }
    }


    private DbCommandInfo BuildSelectAllPagedCommand2008(EntityCommand entCommand) {
      const string SqlSelectAllPaged = @"
WITH SourceQuery AS (
  SELECT {0} 
  FROM {1}
),
NumberedQuery AS (
  SELECT *, Row_Number() OVER ({2}) AS __rownumber
    FROM SourceQuery 
)
SELECT * FROM NumberedQuery
   WHERE __rownumber BETWEEN (@__skiprows + 1) AND (@__skiprows + @__maxrows);
";

      var table = DbModel.LookupDbObject<DbTableInfo>(entCommand.TargetEntityInfo, throwNotFound: true);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList(); 
      var strOrderBy = BuildOrderBy(table.DefaultOrderBy);
      //In paged version, inside OVER clause we must have some ORDER BY clause. If the table has no ORDER BY, then just use PK
      if (string.IsNullOrEmpty(strOrderBy))
        strOrderBy = "ORDER BY " + table.PrimaryKey.KeyColumns.GetSqlNameList();
      var sql = string.Format(SqlSelectAllPaged, strColumns, table.FullName, strOrderBy);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entCommand, table.TableName, "SelectAllPaged");
      var cmdInfo = CreateDbCommandInfo(entCommand, cmdName, table, DbExecutionType.Reader, sql);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns); 
      return cmdInfo;
    }

    private DbCommandInfo BuildSelectAllPagedCommand2012(EntityCommand entCommand) {
      const string SqlSelectAllPaged = @"
SELECT {0} 
  FROM   {1}
  {2}
  OFFSET @__skiprows ROWS
  FETCH NEXT @__maxrows ROWS ONLY; 
";
      var table = DbModel.LookupDbObject<DbTableInfo>(entCommand.TargetEntityInfo, throwNotFound: true);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      string strOrderBy = (table.DefaultOrderBy == null) ? "ORDER BY (SELECT 1)" : BuildOrderBy(table.DefaultOrderBy);
      var sql = string.Format(SqlSelectAllPaged, strColumns, table.FullName, strOrderBy);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entCommand, table.TableName, "SelectAllPaged");
      var cmdInfo = CreateDbCommandInfo(entCommand, cmdName, table, DbExecutionType.Reader, sql);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;
    }

    // 
    public override DbCommandInfo BuildSqlInsertCommand(EntityCommand entityCommand) {
      const string SqlInsertTemplate = @"
INSERT INTO {0} 
  ({1}) 
  VALUES
    ({2}); 
{3} {4}
";
      const string SqlGetIdentityTemplate = "\r\nSET {0} = SCOPE_IDENTITY();";
      const string SqlGetRowVersionTemplate = "\r\nSET {0} = @@DBTS;";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var idClause = string.Empty;
      var rvClause = string.Empty;
      var listColumns = new List<DbColumnInfo>();
      var listValues = new StringList();
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "Insert");
      var dbCmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      foreach (var prm in dbCmdInfo.Parameters) {
        var col = prm.SourceColumn;
        if (col.Flags.IsSet(DbColumnFlags.Identity))
          idClause = string.Format(SqlGetIdentityTemplate, prm.Name);
        if (col.Member.Flags.IsSet(EntityMemberFlags.RowVersion))
          rvClause = string.Format(SqlGetRowVersionTemplate, prm.Name);
        if (!col.Flags.IsSet(DbColumnFlags.NoInsert)) {
          listColumns.Add(col);
          listValues.Add(prm.Name);
        }
      }
      //build SQL
      var strColumns = listColumns.GetSqlNameList();
      var strValues = string.Join(", ", listValues);
      dbCmdInfo.Sql = string.Format(SqlInsertTemplate, table.FullName, strColumns, strValues, idClause, rvClause);
      return dbCmdInfo;
    }

    public override DbCommandInfo BuildSqlUpdateCommand(EntityCommand entityCommand) {
      var hasRowVersion = entityCommand.TargetEntityInfo.Flags.IsSet(EntityFlags.HasRowVersion);
      if (hasRowVersion)
        return BuildSqlUpdateCommandWithConcurrencyCheck(entityCommand);
      else
        return base.BuildSqlUpdateCommand(entityCommand); 
    }

    public override DbCommandInfo BuildSqlDeleteCommand(EntityCommand entityCommand) {
      var hasRowVersion = entityCommand.TargetEntityInfo.Flags.IsSet(EntityFlags.HasRowVersion);
      if (hasRowVersion)
        return BuildSqlDeleteCommandWithConcurrencyCheck(entityCommand);
      else
        return base.BuildSqlDeleteCommand(entityCommand); 
    }

    private DbCommandInfo BuildSqlUpdateCommandWithConcurrencyCheck(EntityCommand entityCommand) {
      const string SqlUpdateTemplate = @"
UPDATE {0}
  SET {1} 
  WHERE {2}; {3}{4}
";

      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      if (entityCommand == null)
        return null;
      var cmdName = this.ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "Update");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      var updateParams = cmdInfo.Parameters.Where(p => !p.SourceColumn.Flags.IsSet(DbColumnFlags.PrimaryKey | DbColumnFlags.NoUpdate));
      // Some tables (like many-to-many link entities) might have no columns to update
      if (!updateParams.Any())
        return null;
      //Build Where expression
      var pkParams = cmdInfo.Parameters.Where(p => p.SourceColumn.Flags.IsSet(DbColumnFlags.PrimaryKey)).ToList();
      var whereParams = pkParams.ToList(); //make a copy
      var rvParam = cmdInfo.Parameters.FirstOrDefault(p => p.TypeInfo.VendorDbType.VendorDbType == (int)SqlDbType.Timestamp);
      Util.Check(rvParam != null, "Internal error in SqlBuilder: failed to find command parameter of type Timestamp. Command: {0}.", entityCommand.CommandName); 
      whereParams.Add(rvParam);       
      var whereExpr = BuildWhereClause(whereParams);
      //Build Update clause
      var updList = new StringList();
      foreach (var prm in updateParams)
        updList.Add("[" + prm.SourceColumn.ColumnName + "] = " + prm.Name);
      var strUpdates = string.Join(", ", updList);
      var strCheckRowCount = BuildRowCountCheckStatement(table, pkParams, entityCommand.Kind);
      var getRowVersion = string.Format("\r\nSET {0} = @@DBTS;", rvParam.Name);
      //Build SQL
      cmdInfo.Sql = string.Format(SqlUpdateTemplate, table.FullName, strUpdates, whereExpr, strCheckRowCount, getRowVersion);
      return cmdInfo;
    }

    //Note: we do not check RowVersion on delete (no need for this) - just checking that @@RowCount is not zero.
    private DbCommandInfo BuildSqlDeleteCommandWithConcurrencyCheck(EntityCommand entityCommand) {
      const string SqlDeleteTemplate = @"
DELETE FROM {0} 
  WHERE {1}; {2}"; 
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      //Load by primary key
      var cmdName = this.ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "Delete");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      var strWhere = BuildWhereClause(cmdInfo.Parameters);
      var strCheckRowCount = BuildRowCountCheckStatement(table, cmdInfo.Parameters, entityCommand.Kind);
      cmdInfo.Sql = string.Format(SqlDeleteTemplate, table.FullName, strWhere, strCheckRowCount);
      return cmdInfo;
    }

    //creates a TSQL statement that raises error with custom message like 'VITA:Concurrency/Update/books.Author/123' (123 is primary key value)
    private string BuildRowCountCheckStatement(DbTableInfo table, List<DbParamInfo> pkParams, EntityCommandKind commandKind) {
      const string sqlCheckRowCount = @"
IF @@RowCount = 0
BEGIN
  DECLARE @msg NVARCHAR(200) = {0};
  RAISERROR(@msg, 11, 111);
END
";
      //Build message expression
      var opType = commandKind == EntityCommandKind.Update ? "Update" : "Delete";
      var msg = "'" + ErrorTagConcurrentUpdateConflict + "/" + opType + "/" + table.FullName + "/' + ";
      var pkExprs = pkParams.Select(p => string.Format("CAST({0} AS NVARCHAR(50))", p.Name));
      var strPks = string.Join(" + ';' + ", pkExprs);
      msg += strPks;
      var result = string.Format(sqlCheckRowCount, msg);
      return result; 
    }

    public override DbCommandInfo BuildSqlSequenceGetNextCommand(DbSequenceInfo sequence) {
      const string SqlTemplate = "SELECT NEXT VALUE FOR {0};";
      //Load by primary key
      var cmdName = sequence.Name + "_GetNextValue";
      var cmdInfo = new DbCommandInfo(DbModel, sequence.Schema, cmdName, null, null);
      cmdInfo.Sql = string.Format(SqlTemplate, sequence.FullName);
      cmdInfo.ExecutionType = DbExecutionType.Scalar;
      return cmdInfo;
    }

    public override DbCommandInfo BuildSelectByKeyArrayCommand(EntityCommand entityCommand) {
      if (DbModel.Config.Options.IsSet(DbOptions.ForceArraysAsLiterals))
        return base.BuildSelectByKeyArrayCommand(entityCommand); 

      const string SqlSelectByFkTemplate = @"
SELECT {0} 
  FROM {1} 
  WHERE {2}
{3}";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var dbKey = DbModel.LookupDbObject<DbKeyInfo>(entityCommand.SelectKey);
      Util.Check(dbKey.KeyColumns.Count == 1, "Cannot construct SelectByKeyArray command for composite keys. Key: {0}", dbKey);
      var keyCols = dbKey.KeyColumns.GetNames(removeUnderscores: true);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectByArrayOf", keyCols);
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, null);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      //build WHERE clause
      var whereTemplate = "\"{0}\" IN (SELECT CAST([Value] AS {1}) FROM {2})";
      var keyCol0 = dbKey.KeyColumns[0].Column;
      string castType = keyCol0.TypeInfo.SqlTypeSpec;
      var whereExpr = string.Format(whereTemplate, keyCol0.ColumnName, castType, cmdInfo.Parameters[0].Name);
      if (!string.IsNullOrWhiteSpace(entityCommand.Filter))
        whereExpr = whereExpr + " AND " + ProcessFilter(entityCommand, table);
      string orderByExpr = null;
      if (dbKey.KeyType == KeyType.PrimaryKey)
        orderByExpr = null;
      else
        orderByExpr = BuildOrderBy(table.DefaultOrderBy);
      var sql = string.Format(SqlSelectByFkTemplate, strColumns, table.FullName, whereExpr, orderByExpr);
      //Damn postgres reformats the SQL in stored proc body and this screws up comparison; so we are careful here
      sql = sql.Trim() + ";";
      cmdInfo.Sql = sql;
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;

    }//method

    public override DbTypeInfo GetDbTypeInfo(Type clrType, int size) {
      if ((clrType.IsArray || clrType.IsGenericType) && clrType.IsListOfDbPrimitive()) {
        //We build this type on the fly - it is too early to do it in DbSqlBuilder constructor
        if (ArrayAsTableDbTypeInfo == null)
          InitTableTypeInfo(); 
        return ArrayAsTableDbTypeInfo;
      }
      var dbType = base.GetDbTypeInfo(clrType, size);
      Util.Check(dbType != null, "Cannot find DbType for CLR type {0}.", clrType);
      return dbType; 
    }


    public override bool ConvertToStoredProc(DbCommandInfo command) {
      const string ProcBodyTemplate =
@"-- Description: {0}
  {1}
  {2}
{3}
";
      const string CreateProcTemplate = @"CREATE PROCEDURE {0} 
{1} 
  AS 
BEGIN
  SET NOCOUNT ON;
  {2}
END
";
      var table = command.Table;
      command.Schema = table.Schema;
      //Build command that creates stored proc and execute it
      var listParams = new StringList();
      foreach (var prm in command.Parameters) {
        var strOut = (prm.Direction & ParameterDirection.Output) != 0 ? " OUTPUT" : string.Empty;
        // Add READONLY for table-type parameters
        var strReadOnly = (prm.TypeInfo.VendorDbType.VendorDbType == (int)SqlDbType.Structured) ? " READONLY" : string.Empty;  
        var prmSpec = "    " + prm.Name + " " + prm.TypeInfo.SqlTypeSpec + strReadOnly + strOut;
        listParams.Add(prmSpec);
      }
      var strParams = string.Join(",\r\n", listParams);
      var desc = command.EntityCommand.Description;
      var tag = DbDriver.GeneratedCrudProcTagPrefix + command.DescriptiveTag;
      command.SourceHash = SourceHasher.ComputeHash(command.FullCommandName, strParams, command.Sql); 
      command.StoredProcBody = string.Format(ProcBodyTemplate, desc, tag, command.Sql, SourceHasher.GetHashLine(command.SourceHash));
      command.StoredProcText = string.Format(CreateProcTemplate, command.FullCommandName, strParams, command.StoredProcBody);
      return true;
    }

    #region ArrayAsTable DbType handling
    public DbTypeInfo ArrayAsTableDbTypeInfo;

    private void InitTableTypeInfo() {
      var msTypeReg = (MsSqlTypeRegistry)this.DbModel.Driver.TypeRegistry;
      var userDefinedVendorType = msTypeReg.UserDefinedVendorDbType;
      var customTableType = DbModel.CustomDbTypes.First(t => t.Name == MsSqlDbDriver.ArrayAsTableTypeName);
      var tableTypeFullName = customTableType.FullName;
      ArrayAsTableDbTypeInfo = new DbTypeInfo(userDefinedVendorType, tableTypeFullName, false, 0, 0, 0);
      ArrayAsTableDbTypeInfo.PropertyToColumnConverter = MsSqlDbDriver.ConvertListToRecordList;
    }

    #endregion


  }//class
}
