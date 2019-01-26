using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Oracle {
  public class OracleSqlBuilder : DbSqlBuilder {
    OracleSqlDialect _oracleDialect; 
    public OracleSqlBuilder(DbModel dbModel, LinqCommandInfo queryInfo) : base(dbModel, queryInfo) {
      _oracleDialect = (OracleSqlDialect)base.SqlDialect; 
    }

    public override SelectExpression PreviewSelect(SelectExpression select, LockType lockType) {
      for(int i=0; i < select.Operands.Count; i++) {
        var outCol = select.Operands[i];
        var newOutCol = CheckSelectOutputColumn(outCol, select); 
        if (newOutCol != outCol) {
          CopyAlias(outCol, newOutCol);
          select.Operands[i] = newOutCol;
        }
      } //for i
      return base.PreviewSelect(select, lockType);
    }

    private Expression CheckSelectOutputColumn(Expression outCol, SelectExpression select) {
      // 1. check precision of decimal column
      // Oracle - old well known bug: InvalidCastException retrieving a high precision decimal
      // ex: 'Select 1.0/3', blows up when trying read: value= reader[0]
      // CLR decimal precision is 28 while OracleDecimal is 38, so it just blows up
      // Workaround - round output values 
      if(outCol.Type == typeof(decimal)) {
        var colExpr = outCol as ColumnExpression;
        if(colExpr != null && colExpr.ColumnInfo.Member.Precision <= 27)
          return outCol; // no need to wrap
        var roundExpr = new SqlFunctionExpression(SqlFunctionType.Round, typeof(decimal), outCol, Expression.Constant(27));
        return roundExpr;
      }
      // 2. Oracle does not allow bool value as output, so 'SELECT (1 > 0)' would fail.
      //    we need to convert it to 1/0
      if (outCol.Type == typeof(bool) && !(outCol is ColumnExpression)) {
        // bools are expressed numeric(1)
        var convExpr = new SqlFunctionExpression(SqlFunctionType.ConvertBoolToBit, typeof(decimal), outCol);
        return convExpr; 
      }
      return outCol; 
    }

    private void CopyAlias(Expression from, Expression to) {
      var sFrom = from as SqlExpression;
      var sTo = to as SqlExpression;
      if(sTo != null && sFrom != null)
        sTo.Alias = sFrom.Alias;
    }


    public override SqlFragment BuildFromClause(SelectExpression selectExpression, IList<TableExpression> tables) {
      // Oracle does not allow empty From, use 'from dual' in this case
      if(tables.Count == 0) 
        return _oracleDialect.SqlFromDual;
      return base.BuildFromClause(selectExpression, tables);
    }

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags;
      if(flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table);
      return sql;
    }

    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      sql.TrimEndingSemicolon(); // somewhat a hack
      // Append returning clause
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var dbType = idCol.Member.DataType.GetIntDbType();
      // we create placeholder based on Id column, only with OUTPUT direction - this results in parameter to return value
      var idPrmPh = new SqlColumnValuePlaceHolder(idCol, ParameterDirection.Output);
      sql.PlaceHolders.Add(idPrmPh);
      var getIdSql = _oracleDialect.SqlReturnIdentityTemplate.Format(idCol.SqlColumnNameQuoted, idPrmPh);
      sql.Append(getIdSql);
      sql.Append(SqlTerms.NewLine);
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      if(limit == null)
        return _oracleDialect.OffsetTemplate.Format(offset);
      else
        return _oracleDialect.OffsetLimitTemplate.Format(offset, limit);
    }

    public override SqlFragment BuildLockClause(SelectExpression selectExpression, LockType lockType) {
      switch(lockType) {
        case LockType.ForUpdate:
          return _oracleDialect.SqlTermLockForUpdate;
        case LockType.SharedRead:
          return null; //nothing
        default:
          return null;
      }

    } //method

    public override SqlFragment BuildSqlForSqlFunctionExpression(SqlFunctionExpression expr) {
      switch(expr.FunctionType) {
        case SqlFunctionType.Concat:
          // there can be multiple args, > 2, can't use template here
          var argSqls = BuildSqls(expr.Operands);
          var list = SqlFragment.CreateList(_oracleDialect.ConcatOperator, argSqls);
          return CompositeSqlFragment.Parenthesize(list);
      }
      return base.BuildSqlForSqlFunctionExpression(expr);
    }

  }
}
