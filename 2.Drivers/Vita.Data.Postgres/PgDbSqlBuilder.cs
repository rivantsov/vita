using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Postgres {

  public class PgDbSqlBuilder : DbSqlBuilder {
    PgDbSqlDialect _pgDialect; 

    public PgDbSqlBuilder(DbModel dbModel, QueryInfo queryInfo): base(dbModel, queryInfo) {
      _pgDialect = (PgDbSqlDialect)dbModel.Driver.SqlDialect;
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

    // ------------- Notes on identity return ------------------------
    // it sounds logical to use 'Returning id INTO @P', but this does not work
    // SqlTemplate SqlTemplateReturnIdentity = new SqlTemplate(" RETURNING {0} INTO {1};");
    // Note: using @p = LastVal(); -does not work, gives error: Commands with multiple queries cannot have out parameters
    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags;
      if(flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table);
      return sql;
    }

    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      sql.TrimEndingSemicolon();
      // Append returning clause
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var dbType = idCol.Member.DataType.GetIntDbType();
      // we create placeholder based on Id column, only with OUTPUT direction - this results in parameter to return value
      var idPrmPh = new SqlColumnValuePlaceHolder(idCol, ParameterDirection.Output);
      sql.PlaceHolders.Add(idPrmPh);
      var getIdSql = _pgDialect.SqlCrudTemplateReturningIdentity.Format(idCol.SqlColumnNameQuoted);
      sql.Append(getIdSql);
      sql.Append(SqlTerms.NewLine);
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      var sqlLimit = limit == null ? null : _pgDialect.SqlLimitTemplate.Format(limit);
      var sqlOffset = offset == null ? null : _pgDialect.SqlOffsetTemplate.Format(offset);
      if(sqlLimit == null)
        return sqlOffset;
      if(sqlOffset == null)
        return sqlLimit;
      return new CompositeSqlFragment(sqlLimit, sqlOffset);
    }

    // special form, for array in parameter
    SqlTemplate InArrayTemplate = new SqlTemplate("{0} = ANY({1})", SqlPrecedence.LowestPrecedence);

    public override SqlFragment BuildSqlForSqlFunctionExpression(SqlFunctionExpression expr) {
      switch(expr.FunctionType) {
        case SqlFunctionType.InArray:
          var forceLiterals = this.QueryInfo.Options.IsSet(QueryOptions.NoParameters);
          if(forceLiterals)
            break; //do not do special form
          // check special form is needed
          var pv = expr.Operands[0];
          var parameter = expr.Operands[1] as ExternalValueExpression;
          if(parameter != null && parameter.IsList) {
            var valueSql = BuildLinqExpressionSql(pv);
            var prmSql = BuildLinqExpressionSql(parameter);
            return InArrayTemplate.Format(valueSql, prmSql);
          }
          break;
      }
      return base.BuildSqlForSqlFunctionExpression(expr);
    }


  }
}
