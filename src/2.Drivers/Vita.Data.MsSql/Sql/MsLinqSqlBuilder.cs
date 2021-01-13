using System.Collections.Generic;
using System.Linq.Expressions;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;

namespace Vita.Data.MsSql {

  public class MsLinqSqlBuilder : DbLinqSqlBuilder {
    MsSqlDialect _msDialect;

    // maxParamsCount is 2300 for MS SQL, but we're being a bit cautious here
    public MsLinqSqlBuilder(DbModel dbModel, LinqCommand command) : base(dbModel, command) {
      _msDialect = (MsSqlDialect)dbModel.Driver.SqlDialect;
    }

    public override SelectExpression PreviewSelect(SelectExpression select, LockType lockType) {
      select = base.PreviewSelect(select, lockType);
      // Use TOP if there's limit, no offset, and there's no order by - to exclude search queries; 
      // searches do have OrderBy, and for searches we want to use standard clause with Skip-Offset
      if(select.Limit != null && (select.Offset == null || Linq.LinqExpressionHelper.IsConstZero(select.Offset))
                   && select.OrderBy.Count == 0) {
        select.Flags |= SelectExpressionFlags.MsSqlUseTop;
        // Fake order-by is needed only with full Fetch Next/Offset syntax 
        select.Flags &= ~SelectExpressionFlags.NeedsFakeOrderBy;
      }
      // SQL Server does not allow bool values in SELECT list. For ex, the following results in error: 
      //   SELECT (book.Price > 10) AS OverPriced, ..... 
      //  So we need to find output exprs like this and wrap them like: 
      //   SELECT IIF(book.Price > 10, 1, 0) AS OverPriced, ...
      for (int i = 0; i < select.Operands.Count; i++) {
        var outExpr = select.Operands[i];
        if (outExpr.Type == typeof(bool) && !(outExpr is ColumnExpression)) {
          var wrappedExpr = new SqlFunctionExpression(SqlFunctionType.Iif, typeof(int), 
                outExpr, ExpressionMaker.Const1Int, ExpressionMaker.Const0Int);
          select.Operands[i] = wrappedExpr; 
        }
      }
      return select; 
    }

    // Inject TOP() clause
    public override SqlFragment BuildSelectOutputClause(SelectExpression select) {
      var flist = new List<SqlFragment>();
      flist.Add(SqlTerms.Select);
      if (select.Flags.IsSet(SelectExpressionFlags.MsSqlUseTop)) {
        var limitSql = BuildLinqExpressionSql(select.Limit);
        var topSql = _msDialect.TopTemplate.Format(limitSql);
        flist.Add(topSql);
      }
      if (select.Flags.IsSet(SelectExpressionFlags.Distinct))
        flist.Add(SqlTerms.Distinct);
      var outCols = BuildSelectOutputList(select);
      flist.Add(outCols);
      return SqlFragment.CreateList(SqlTerms.Space, flist);
    }

    public override SqlFragment BuildTableForFrom(TableExpression te) {
      var tableRef = base.BuildTableForFrom(te);
      SqlFragment hint = null;
      switch(te.LockType) {
        case LockType.ForUpdate:  hint = _msDialect.WithUpdateLockHint; break;
        case LockType.NoLock:     hint = _msDialect.WithNoLockHint;   break;
        case LockType.SharedRead:   break; //turns out we do not need any hint here if we run in Snapshot level
      }
      //Note: for MS SQL, table alias (if present) goes before the hint ( ... FROM Tbl t WITH(NOLOCK) ...), so it works as coded
      if(hint != null)
        tableRef = new CompositeSqlFragment(tableRef, hint);
      return tableRef;
    }

    public override SqlFragment BuildSqlForStandardExpression(Expression expr) {
      // MS SQL does NOT have 'True' constant, so it should be replaced with 1 or 0
      if (expr.NodeType == ExpressionType.Constant && expr.Type == typeof(bool)) {
        var ce = (ConstantExpression)expr;
        var boolValue = (bool)ce.Value;
        return boolValue ? SqlTerms.One : SqlTerms.Zero;
      }
      return base.BuildSqlForStandardExpression(expr);
    }

    public override SqlFragment BuildSqlForSqlFunctionExpression(SqlFunctionExpression expr) {
      switch(expr.FunctionType) {
        case SqlFunctionType.Concat:
          // there can be multiple args, > 2, can't use template here
          var argSqls = BuildSqls(expr.Operands);
          var args = SqlFragment.CreateList(SqlTerms.Comma, argSqls);
          return _msDialect.ConcatTemplate.Format(args);
      }
      return base.BuildSqlForSqlFunctionExpression(expr);
    }

    public override SqlFragment BuildLimitClause(SelectExpression selectExpression) {
      if (selectExpression.Flags.IsSet(SelectExpressionFlags.MsSqlUseTop))
        return null; 
      return base.BuildLimitClause(selectExpression);
    }

    protected override SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      if(limit == null)
        return _msDialect.OffsetTemplate.Format(offset);
      else
        return _msDialect.OffsetLimitTemplate.Format(offset, limit);
    }

  }//class
}
