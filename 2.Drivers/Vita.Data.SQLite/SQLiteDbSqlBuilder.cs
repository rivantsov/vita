using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Data.SQLite {
  public class SQLiteDbSqlBuilder : DbSqlBuilder {
    SQLiteDbSqlDialect _dialect; 

    public SQLiteDbSqlBuilder(DbModel dbModel, QueryInfo queryInfo): base(dbModel, queryInfo) {
      _dialect = (SQLiteDbSqlDialect)dbModel.Driver.SqlDialect; 
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      if(limit == null)
        return _dialect.OffsetTemplate.Format(offset);
      else
        return _dialect.OffsetLimitTemplate.Format(offset, limit);
    }

    public override SqlFragment BuildOrderByMember(OrderByExpression obExpr) {
      var baseFr = base.BuildOrderByMember(obExpr);
      if (obExpr.ColumnExpression.Type == typeof(string))
        return new CompositeSqlFragment(baseFr, _dialect.SqlCollateNoCase);
      return baseFr; 
    }

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      if (table.Entity.Flags.IsSet(EntityFlags.HasIdentity))
        sql.ResultProcessor = this._identityReader;
      return sql;
    }

    // Inserted identity is returned by extra select; command postprocessor IdentityReader gets it from DataReader
    // and puts it into EntityRecord
    IdentityReader _identityReader = new IdentityReader();

    class IdentityReader : IDataCommandResultProcessor {
      public object ProcessResult(DataCommand command) {
        command.RowCount = 1;
        var conn = command.Connection; 
        var idCmd = conn.DbConnection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        idCmd.Transaction = conn.DbTransaction;
        var idValue = idCmd.ExecuteScalar(); //it is Int64
        Util.Check(idValue != null, "Failed to retrieve identity value for inserted row, returned value: " + idValue);
        var rec = command.Records[0]; //there must be a single record
        var idMember = rec.EntityInfo.IdentityMember;
        if (idValue.GetType() != idMember.DataType)
          idValue = ConvertHelper.ChangeType(idValue, idMember.DataType);
        rec.SetValueDirect(idMember, idValue);
        return 1; 
      }

    }


  }
}
