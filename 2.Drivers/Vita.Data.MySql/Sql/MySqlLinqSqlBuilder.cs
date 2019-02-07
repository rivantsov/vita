using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Runtime;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Data.MySql {
  public class MySqlLinqSqlBuilder : DbLinqSqlBuilder {

    private MySqlDialect _myDialect; 

    public MySqlLinqSqlBuilder(DbModel dbModel, ExecutableLinqCommand command) : base(dbModel, command) {
      _myDialect = (MySqlDialect) dbModel.Driver.SqlDialect; 
    }

    public override SelectExpression PreviewSelect(SelectExpression select, LockType lockType) {
      // Fix for views matching. 
      // force aliases on all tables. The reason is view generation; when view is normalized, 
      // and table in FROM has no alias, then all columns in output clause a listed prefixed with table name
      // - which screws up view version comparison in schema update. 
      // another thing - it adds explicit alias to all output columns (same as column name)
      select = base.PreviewSelect(select, lockType);
      if (select.Command.IsView) {
        int cnt = 0;
        foreach(var t in select.Tables)
          if(string.IsNullOrEmpty(t.Alias))
            t.Alias = "tx" + (cnt++) + '$'; // x is to avoid clashing with existing aliases
        // fix output columns
        foreach(var outCol in select.Columns) {
          if(outCol.Alias == null)
            outCol.Alias = outCol.ColumnInfo.ColumnName;
        }
      }
      return select;
    }

    public override SqlFragment BuildLockClause(SelectExpression selectExpression, LockType lockType) {
      switch(lockType) {
        case LockType.ForUpdate:
          return _myDialect.SqlTermLockForUpdate;
        case LockType.SharedRead:
          return _myDialect.SqlTermLockInShareMode;
        default:
          return null;
      }
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      if(limit == null)
        return _myDialect.OffsetTemplate.Format(offset);
      else
        return _myDialect.OffsetLimitTemplate.Format(offset, limit);
    }

  }
}
