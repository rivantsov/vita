using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Model;

namespace Vita.Data.SQLite {
  public class SQLiteDbSqlBuilder : DbSqlBuilder {
    public SQLiteDbSqlBuilder(DbModel dbModel)   : base(dbModel) {

    }

    protected override DbSqlBuilder.SqlLimitClause GetSqlLimitClause(string count) {
      return new SqlLimitClause() { TopClause = string.Empty };
    } 

    public override DbCommandInfo CreateDbCommandInfo(EntityCommand entityCommand, string name, DbTableInfo mainTable, DbExecutionType executionType, string sql) {
      var cmdInfo = base.CreateDbCommandInfo(entityCommand, name, mainTable, executionType, sql);
      var ent = entityCommand.TargetEntityInfo;
      if (cmdInfo.Kind == EntityCommandKind.Insert && ent.Flags.IsSet(EntityFlags.HasIdentity)) {
        //Add actions to read identity value
        var idPrm = cmdInfo.Parameters.FirstOrDefault(p => p.SourceColumn.Flags.IsSet(DbColumnFlags.Identity));
        if (idPrm != null) 
          cmdInfo.PostUpdateActions.Add(GetLastRowId); 
      }
      return cmdInfo; 
    }

    private static void GetLastRowId(DataConnection conn, IDbCommand cmd, Vita.Entities.Runtime.EntityRecord rec) {
      var idCmd = conn.DbConnection.CreateCommand();
      idCmd.CommandText = "SELECT last_insert_rowid();";
      idCmd.Transaction = conn.DbTransaction;
      var id = idCmd.ExecuteScalar(); //it is Int64
      Util.Check(id != null, "Failed to retrieve identity value for inserted row. Command: " + idCmd.CommandText);
      var member = rec.EntityInfo.Members.First(m => m.Flags.IsSet(EntityMemberFlags.Identity)); //idPrm.SourceColumn.Member;
      if (member.DataType != id.GetType())
        id = Convert.ChangeType(id, member.DataType);
      rec.SetValueDirect(member, id);
      idCmd.Connection = null; //to dispose prepared command
    }

    protected override string FormatOrderByEntry(DbKeyColumnInfo colInfo) {
      return base.FormatOrderByEntry(colInfo) + " COLLATE NOCASE";
    }

    public override DbCommandInfo BuildSelectAllPagedCommand(EntityCommand entityCommand) {
      const string SqlSelectAllPaged = @"
SELECT {0}
FROM   {1}
{2}
LIMIT @__maxRows
OFFSET @__skipRows
;";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      string strOrderBy = BuildOrderBy(table, table.DefaultOrderBy); //might be empty
      var sql = string.Format(SqlSelectAllPaged, strColumns, table.FullName, strOrderBy);
      var cmdName = ModelConfig.NamingPolicy.GetDbCommandName(entityCommand, table.TableName, "SelectAllPaged");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, sql);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;
    }
  }//class
}
