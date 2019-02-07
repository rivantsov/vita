using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Postgres {

  public class PgLinqSqlBuilder : DbLinqSqlBuilder {
    PgSqlDialect _pgDialect; 

    public PgLinqSqlBuilder(DbModel dbModel, ExecutableLinqCommand command): base(dbModel, command) {
      _pgDialect = (PgSqlDialect)dbModel.Driver.SqlDialect;
    }


    public override SqlFragment BuildLockClause(SelectExpression selectExpression, LockType lockType) {
      switch(lockType) {
        case LockType.ForUpdate:
          return _pgDialect.SqlLockForUpdate;
        case LockType.SharedRead:
          return _pgDialect.SqlLockInShareMode;
        default:
          return null;
      }
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      if(limit == null)
        return this.SqlDialect.OffsetTemplate.Format(offset);
      else
        return this.SqlDialect.OffsetLimitTemplate.Format(offset, limit);
    }

  }
}
