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
using MySql.Data.MySqlClient;

namespace Vita.Data.MySql {
  
  public partial class MySqlDbSqlBuilder : DbSqlBuilder {

    public MySqlDbSqlBuilder(DbModel dbModel) : base(dbModel) { }

    protected override SqlLimitClause GetSqlLimitClause(string count) {
      return new SqlLimitClause() { LimitClause = "Limit " + count};
    }

    public override DbCommandInfo BuildSelectAllPagedCommand(EntityCommand entCommand) {
      const string SqlSelectAllPaged = @"
SELECT {0} FROM {1}
  {2}
  LIMIT {3}, {4};
";

      var table = DbModel.LookupDbObject<DbTableInfo>(entCommand.TargetEntityInfo, throwNotFound: true);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      var strOrderBy = BuildOrderBy(table, table.DefaultOrderBy); //might be empty
      var cmdName = ModelConfig.NamingPolicy.GetDbCommandName(entCommand, table.TableName, "SelectAllPaged");
      var cmdInfo = CreateDbCommandInfo(entCommand, cmdName, table, DbExecutionType.Reader, string.Empty);
      var skipPrm = cmdInfo.Parameters[0].Name;
      var takePrm = cmdInfo.Parameters[1].Name;
      cmdInfo.Sql = string.Format(SqlSelectAllPaged, strColumns, table.FullName, strOrderBy, skipPrm, takePrm);
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
{3}
";
      bool useStoredProc = this.DbModel.Config.Options.IsSet(DbOptions.UseStoredProcs);
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var idClause = string.Empty;
      var listColumns = new List<DbColumnInfo>();
      var listValues = new StringList();
      var cmdName = ModelConfig.NamingPolicy.GetDbCommandName(entityCommand, table.TableName, "Insert");
      var dbCmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      foreach (var prm in dbCmdInfo.Parameters) {
        var col = prm.SourceColumn;
        if (!col.Flags.IsSet(DbColumnFlags.NoInsert)) {
          listColumns.Add(col);
          listValues.Add(prm.Name);
        }
        // identity
        if (col.Flags.IsSet(DbColumnFlags.Identity)) {
          if (useStoredProc)
            //append to stored proc
            idClause = string.Format("SET {0} = LAST_INSERT_ID();", prm.Name);
          else 
            dbCmdInfo.PostUpdateActions.Add((conn, cmd, rec) => {
              var idCmd = conn.DbConnection.CreateCommand();
              idCmd.CommandText = "Select LAST_INSERT_ID();";
              idCmd.Transaction = conn.DbTransaction;
              var id = conn.Database.ExecuteDbCommand(idCmd, conn, DbExecutionType.Scalar); //it is decimal
              var intId = Convert.ChangeType(id, prm.SourceColumn.Member.DataType);
              rec.SetValueDirect(prm.SourceColumn.Member, intId);
            });
        }//if identity
      }
     // this.ModelConfig.Options.IsSet(DbOptions.us)
      //build SQL
      var strColumns = listColumns.GetSqlNameList();
      var strValues = string.Join(", ", listValues);
      dbCmdInfo.Sql = string.Format(SqlInsertTemplate, table.FullName, strColumns, strValues, idClause);
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
      var cmdName = this.ModelConfig.NamingPolicy.GetDbCommandName(entityCommand, table.TableName, "Update");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      var updateParams = cmdInfo.Parameters.Where(p => !p.SourceColumn.Flags.IsSet(DbColumnFlags.PrimaryKey | DbColumnFlags.NoUpdate));
      // Some tables (like many-to-many link entities) might have no columns to update
      if (!updateParams.Any())
        return null;
      //Build Where expression
      var pkParams = cmdInfo.Parameters.Where(p => p.SourceColumn.Flags.IsSet(DbColumnFlags.PrimaryKey)).ToList();
      var whereParams = pkParams.ToList(); //make a copy
      var rvParam = cmdInfo.Parameters.First(p => p.TypeInfo.VendorDbType.VendorDbType == (int) MySqlDbType.Timestamp);
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
      var cmdName = this.ModelConfig.NamingPolicy.GetDbCommandName(entityCommand, table.TableName, "Delete");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.NonQuery, null);
      var strWhere = BuildWhereClause(cmdInfo.Parameters);
      var strCheckRowCount = BuildRowCountCheckStatement(table, cmdInfo.Parameters, entityCommand.Kind);
      cmdInfo.Sql = string.Format(SqlDeleteTemplate, table.FullName, strWhere, strCheckRowCount);
      return cmdInfo;
    }

    //creates a TSQL statement that raises error with custom message like 'VITA:Concurrency/Update/books.Author/123' (123 is primary key value)
    // TODO: This is non-sense, copied from MS SQL, need to write real one
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

    //Note: as of May/13, MySql does not preserve comments preceding SP header; they are going to patch this.
    // For now, we put tag in a comment right after BEGIN
    public override bool ConvertToStoredProc(DbCommandInfo command) {
      var table = command.Table;
      string ProcBodyTemplate =
@"BEGIN
{0}
  -- Description: {1}
  {2}
{3}
END";
      string CreateProcTemplate =
@"CREATE PROCEDURE {0}(
  {1}) 
  SQL SECURITY INVOKER
{2}
";
      command.Schema = table.Schema;
      //Build command that creates stored proc and execute it
      var listParams = new StringList();
      foreach (var prm in command.Parameters) {
        var strOut = (prm.Direction & ParameterDirection.Output) != 0 ? "OUT " : string.Empty;
        var prmSpec = string.Format("    {0}{1} {2}", strOut, prm.Name, prm.TypeInfo.SqlTypeSpec);
        listParams.Add(prmSpec);
      }
      var strParams = string.Join(",\r\n", listParams);
      var tag = DbDriver.GeneratedCrudProcTagPrefix + command.DescriptiveTag;
      command.SourceHash = SourceHasher.ComputeHash(command.FullCommandName, strParams, command.Sql);
      command.StoredProcBody = string.Format(ProcBodyTemplate, tag, command.Description, command.Sql, SourceHasher.GetHashLine(command.SourceHash));
      command.StoredProcText = string.Format(CreateProcTemplate, command.FullCommandName, strParams, command.StoredProcBody);
      return true;
    }


  }//class
}
