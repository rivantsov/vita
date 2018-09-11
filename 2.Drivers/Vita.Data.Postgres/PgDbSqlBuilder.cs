using System;
using System.Collections.Generic;
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
    PgDbSqlDialect _dialect; 

    public PgDbSqlBuilder(DbModel dbModel, QueryInfo queryInfo): base(dbModel, queryInfo) {
      _dialect = (PgDbSqlDialect)dbModel.Driver.SqlDialect; 
    }


    public override SqlFragment BuildLockClause(SelectExpression selectExpression, LockType lockType) {
      switch(lockType) {
        case LockType.ForUpdate:
          return _dialect.SqlLockForUpdate;
        case LockType.SharedRead:
          return _dialect.SqlLockInShareMode;
        default:
          return null;
      }
    }

    // ------------- Notes on identity return ------------------------
    // it sounds logical to use 'Returning id INTO @P', but this does not work
    // SqlTemplate SqlTemplateReturnIdentity = new SqlTemplate(" RETURNING {0} INTO {1};");
    // Note: using @p = LastVal(); -does not work, gives error: Commands with multiple queries cannot have out parameters

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      if(!table.Entity.Flags.IsSet(EntityFlags.HasIdentity)) 
        return base.BuildCrudInsertOne(table, record);
      // slightly modified base Insert method, with added 'RETURNING IdCol'
      // list of column names
      var insertCols = GetColumnsToInsert(table, record);
      var insertColsSqls = insertCols.Select(c => c.SqlColumnNameQuoted).ToList();
      var colListSql = SqlFragment.CreateList(SqlTerms.Comma, insertColsSqls);
      // values
      var placeHolders = new List<SqlPlaceHolder>();
      var colValues = insertCols.Select(c => placeHolders.AddColumnValueRef(c)).ToArray();
      var valuesFragm = CompositeSqlFragment.Parenthesize(SqlFragment.CreateList(SqlTerms.Comma, colValues));
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var sql = _dialect.SqlCrudTemplateInsertReturnIdentity.Format(table.SqlFullName, colListSql, 
                     valuesFragm, idCol.SqlColumnNameQuoted);
      var stmt = new SqlStatement(sql, placeHolders, DbExecutionType.NonQuery);
      // add output parameter that will return the id value
      var idPrmPh = stmt.PlaceHolders.AddParamRef(idCol.TypeInfo.StorageType, System.Data.ParameterDirection.Output, idCol);
      return stmt;
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      var sqlLimit = limit == null ? null : _dialect.SqlLimitTemplate.Format(limit);
      var sqlOffset = offset == null ? null : _dialect.SqlOffsetTemplate.Format(offset);
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
