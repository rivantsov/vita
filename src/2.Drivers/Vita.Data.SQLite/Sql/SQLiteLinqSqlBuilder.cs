using System;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;

namespace Vita.Data.SQLite {
  public class SQLiteLinqSqlBuilder : DbLinqSqlBuilder {
    SQLiteDbSqlDialect _dialect; 

    public SQLiteLinqSqlBuilder(DbModel dbModel, LinqCommand command): base(dbModel, command) {
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


  }
}
