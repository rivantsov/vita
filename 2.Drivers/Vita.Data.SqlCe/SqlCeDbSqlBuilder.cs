using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data; 

using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using System.Data.SqlServerCe; 

namespace Vita.Data.SqlCe {
  public class SqlCeDbSqlBuilder : DbSqlBuilder {
    public SqlCeDbSqlBuilder(DbModel dbModel) : base(dbModel) { }

    public override DbCommandInfo BuildSelectAllPagedCommand(EntityCommand entityCommand) {
      const string SqlSelectAllPaged = @"
SELECT {0}
  FROM {1}
  {2}
  OFFSET @__skiprows ROWS
  FETCH NEXT @__maxrows ROWS ONLY;
";

      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      //Build column list
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectAllPaged");
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      //Build Order by
      string strOrderBy = string.Empty; 
      if (table.DefaultOrderBy == null) 
          strOrderBy = "ORDER BY " + table.PrimaryKey.KeyColumns.GetSqlNameList() ;
      else 
        strOrderBy = BuildOrderBy(table, table.DefaultOrderBy);
      var sql = string.Format(SqlSelectAllPaged, strColumns, table.FullName, strOrderBy);
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, sql);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;
    }

    public override DbCommandInfo CreateDbCommandInfo(EntityCommand entityCommand, string name, DbTableInfo mainTable, DbExecutionType executionType, string sql) {
      var cmdInfo = base.CreateDbCommandInfo(entityCommand, name, mainTable, executionType, sql);
      var ent = entityCommand.TargetEntityInfo;
      if (cmdInfo.Kind == EntityCommandKind.Insert && ent.Flags.IsSet(EntityFlags.HasIdentity)) {
        //Add actions to read identity value
        var idPrm = cmdInfo.Parameters.FirstOrDefault(p => p.SourceColumn.Flags.IsSet(DbColumnFlags.Identity));
        if (idPrm != null) {
          cmdInfo.PostUpdateActions.Add((conn, cmd, rec) => {
            var idCmd = conn.DbConnection.CreateCommand();
            idCmd.CommandText = "Select @@IDENTITY;";
            idCmd.Transaction = conn.DbTransaction;
            var id =  conn.Database.ExecuteDbCommand(idCmd, conn, DbExecutionType.Scalar); //it is decimal
            var intId = Convert.ChangeType(id, idPrm.SourceColumn.Member.DataType);
            rec.SetValueDirect(idPrm.SourceColumn.Member, intId); 
          });
        }//if IdPrm ...      
      }
      return cmdInfo; 
    }//method 

  }//class

}
