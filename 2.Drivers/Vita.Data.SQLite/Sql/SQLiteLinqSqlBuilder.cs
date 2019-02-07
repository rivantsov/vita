using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Data.SQLite {
  public class SQLiteLinqSqlBuilder : DbLinqSqlBuilder {
    SQLiteDbSqlDialect _dialect; 

    public SQLiteLinqSqlBuilder(DbModel dbModel, ExecutableLinqCommand command): base(dbModel, command) {
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
