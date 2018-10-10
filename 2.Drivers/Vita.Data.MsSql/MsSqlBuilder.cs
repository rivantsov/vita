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
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.MsSql {

  public class MsSqlBuilder : DbSqlBuilder {
    MsSqlDialect _msDialect; 

    // maxParamsCount is 2300 for MS SQL, but we're being a bit cautious here
    public MsSqlBuilder(DbModel dbModel, QueryInfo queryInfo) : base(dbModel, queryInfo) {
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
        case SqlFunctionType.InArray:
          // When array value is sent in parameter for IN expr (ex: 'SomeCol IN (@P)'), it is sent as table-type param,
          // and IN expr should be written like '.. IN (SELECT V FROM @P). We do it ONLY if array is actually sent 
          // as parameter, and not replaced by literal comma-list of values; this can happen if special Options flag is set;
          //  this flag is used when translating DB Views which cannot have parameters
          var forceLiterals = this.QueryInfo.Options.IsSet(QueryOptions.NoParameters);
          if(forceLiterals)
            break; //do not do special form
          // check special form is needed
          var pv = expr.Operands[0];
          var parameter = expr.Operands[1] as ExternalValueExpression;
          if (parameter != null && parameter.IsList)
            return BuildValueInArrayParamSql(pv, parameter);
          break;
        case SqlFunctionType.Concat:
          // there can be multiple args, > 2, can't use template here
          var argSqls = BuildSqls(expr.Operands);
          var args = SqlFragment.CreateList(SqlTerms.Comma, argSqls);
          return _msDialect.ConcatTemplate.Format(args);
      }
      return base.BuildSqlForSqlFunctionExpression(expr);
    }

    private SqlFragment BuildValueInArrayParamSql(Expression value, ExternalValueExpression parameter) {
      var valueSql = BuildLinqExpressionSql(value); 
      var prmSql = BuildLinqExpressionSql(parameter);
      // We use Sql_variant column in table UDT that holds list. It was found that it causes index scan instead of seek
      // So we add CAST here
      var elType = parameter.ListElementType;
      var typeDef = base.Driver.TypeRegistry.FindStorageType(elType, false);
      if (typeDef != null) {
        var sqlDbType = (SqlDbType)typeDef.CustomDbType;
        var typeName = new TextSqlFragment(sqlDbType.ToString());
        return _msDialect.InArrayTemplateTyped.Format(valueSql, typeName, prmSql);
      } else
        return _msDialect.InArrayTemplateUntyped.Format(valueSql, prmSql);
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

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags; 
      if (flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table); 
      if (flags.IsSet(EntityFlags.HasRowVersion))
        AppendRowVersionCheckReturn(sql, table, record); 
      return sql;
    }

    public override SqlStatement BuildCrudUpdateOne(DbTableInfo table, EntityRecord rec, ISqlValueFormatter valueFormatter) {
      var sql = base.BuildCrudUpdateOne(table, rec, valueFormatter);
      if (table.Entity.Flags.IsSet(EntityFlags.HasRowVersion))
        AppendRowVersionCheckReturn(sql, table, rec);
      return sql; 
    }

    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var idPrmPh = sql.PlaceHolders.AddParamRef(idCol.TypeInfo.StorageType, System.Data.ParameterDirection.Output, idCol);
      var getIdSql = _msDialect.SqlGetIdentityTemplate.Format(idPrmPh);
      sql.Append(getIdSql);
      sql.Append(SqlTerms.NewLine);
    }

    private void AppendRowVersionCheckReturn(SqlStatement sql, DbTableInfo table, EntityRecord record) {
      var rvCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.RowVersion));
      if (record.Status == EntityStatus.Modified) {
        // update row count check
        var tag = new TextSqlFragment($"'ConcurrentUpdate/{table.Entity.Name}/{record.PrimaryKey.ValuesToString()}'");
        var checkRowsSql = _msDialect.SqlCheckRowCountIsOne.Format(tag);
        sql.Append(checkRowsSql);
      }
      // return RowVersion in parameter
      var rvPrm = sql.PlaceHolders.AddParamRef(rvCol.TypeInfo.StorageType, System.Data.ParameterDirection.Output, rvCol);
      var getRvSql = _msDialect.SqlGetRowVersionTemplate.Format(rvPrm);
      sql.Append(getRvSql);
      sql.Append(SqlTerms.NewLine);
    }

  }//class
}
