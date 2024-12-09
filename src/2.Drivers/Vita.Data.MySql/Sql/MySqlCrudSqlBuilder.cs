using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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

namespace Vita.Data.MySql {
  public class MySqlCrudSqlBuilder : DbCrudSqlBuilder {
    private MySqlDialect _myDialect;

    public MySqlCrudSqlBuilder(DbModel dbModel): base(dbModel) {
      _myDialect = (MySqlDialect)dbModel.Driver.SqlDialect;
    }

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags;
      if(flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table);
      return sql;
    }

    // Inserted identity is returned by extra select; command postprocessor IdentityReader gets it from DataReader
    // and puts it into EntityRecord
    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      sql.Append(_myDialect.SqlSelectIdentity); // returned value is decimal!
      sql.Append(SqlTerms.NewLine);
      sql.ExecutionType = DbExecutionType.Reader;
      sql.ResultProcessor = this._identityReader;
    }

    IdentityReader _identityReader = new IdentityReader();

    class IdentityReader : IDataCommandResultProcessor {

      public object ProcessResults(DataCommand command) {
        var reader = command.Result as IDataReader;
        Util.Check(reader.Read(), "Identity reader error: command did not return a record with identity.");
        var idValue = reader[0];
        var rec = command.Records[0]; //there must be a single record
        var idMember = rec.EntityInfo.IdentityMember;
        if(idValue.GetType() != idMember.DataType)
          idValue = ConvertHelper.ChangeType(idValue, idMember.DataType);
        rec.SetValueDirect(idMember, idValue);
        reader.Close();
        return reader;
      }
      public async Task<object> ProcessResultsAsync(DataCommand command) {
        var reader = command.Result as DbDataReader;
        Util.Check(await reader.ReadAsync(), "Identity reader error: command did not return a record with identity.");
        var idValue = reader[0];
        var rec = command.Records[0]; //there must be a single record
        var idMember = rec.EntityInfo.IdentityMember;
        if (idValue.GetType() != idMember.DataType)
          idValue = ConvertHelper.ChangeType(idValue, idMember.DataType);
        rec.SetValueDirect(idMember, idValue);
        reader.Close();
        return reader;
      }
    } //nested class

  } //class
}
