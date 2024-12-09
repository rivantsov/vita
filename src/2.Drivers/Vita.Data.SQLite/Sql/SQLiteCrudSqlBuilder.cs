using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Data.SQLite {

  public class SQLiteCrudSqlBuilder: DbCrudSqlBuilder {
    public SQLiteCrudSqlBuilder(DbModel dbModel) : base(dbModel) {

    }

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      if(table.Entity.Flags.IsSet(EntityFlags.HasIdentity))
        sql.ResultProcessor = this._identityReader;
      return sql;
    }

    // Inserted identity is returned by extra select; command postprocessor IdentityReader gets it from DataReader
    // and puts it into EntityRecord
    IdentityReader _identityReader = new IdentityReader();

    class IdentityReader : IDataCommandResultProcessor {
      public object ProcessResults(DataCommand command) {
        command.RowCount = 1;
        var conn = command.Connection;
        var idCmd = conn.DbConnection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        idCmd.Transaction = conn.DbTransaction;
        var idValue = idCmd.ExecuteScalar(); //it is Int64
        Util.Check(idValue != null, "Failed to retrieve identity value for inserted row, returned value: " + idValue);
        var rec = command.Records[0]; //there must be a single record
        var idMember = rec.EntityInfo.IdentityMember;
        if(idValue.GetType() != idMember.DataType)
          idValue = ConvertHelper.ChangeType(idValue, idMember.DataType);
        rec.SetValueDirect(idMember, idValue);
        return 1;
      }

      public async Task<object> ProcessResultsAsync(DataCommand command) {
        command.RowCount = 1;
        var conn = command.Connection;
        var idCmd = conn.DbConnection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        idCmd.Transaction = conn.DbTransaction;
        var idValue = await idCmd.ExecuteScalarAsync(); //it is Int64
        Util.Check(idValue != null, "Failed to retrieve identity value for inserted row, returned value: " + idValue);
        var rec = command.Records[0]; //there must be a single record
        var idMember = rec.EntityInfo.IdentityMember;
        if (idValue.GetType() != idMember.DataType)
          idValue = ConvertHelper.ChangeType(idValue, idMember.DataType);
        rec.SetValueDirect(idMember, idValue);
        return 1;
      }

    }


  } //class
}
