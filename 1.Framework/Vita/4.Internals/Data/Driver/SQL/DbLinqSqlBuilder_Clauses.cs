using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using System.Linq.Expressions;
using Vita.Data.Linq.Translation;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  public partial class DbLinqSqlBuilder {

    public virtual SqlFragment BuildSelectOutputClause(SelectExpression select) {
      var flist = new List<SqlFragment>();
      flist.Add(SqlTerms.Select);
      if (select.Flags.IsSet(SelectExpressionFlags.Distinct))
        flist.Add(SqlTerms.Distinct);
      var outCols = BuildSelectOutputList(select);
      flist.Add(outCols);
      return SqlFragment.CreateList(SqlTerms.Space, flist);
    }

    public virtual SqlFragment BuildSelectOutputList(SelectExpression select) {
      var outCols = new List<SqlFragment>();
      if (select.Flags.IsSet(SelectExpressionFlags.Distinct)) {
        //RI: changed code
        var g = select.Group[0];
        foreach(var col in g.Columns) {
          var sqlCol = GetColumnRefSql(col, forOutput: true);
          outCols.Add(sqlCol);
        }
        var outColsFragment = SqlFragment.CreateList(SqlTerms.Comma, outCols);
        return outColsFragment;
      }
      //Regular
      var ops = select.GetOperands().ToList();
      // No explicit columns
      if(ops.Count == 0) {
        if (select.Group.Count > 0)
          return this.SqlDialect.SqlNullAsEmpty;
        else
          return SqlTerms.Star; 
      }
      foreach(var outExpr in ops) {
        var outSql = BuildLinqExpressionSql(outExpr);
        var alias = GetAlias(outExpr);
        if(!string.IsNullOrEmpty(alias)) {
          var aliasPart = new TextSqlFragment(SqlDialect.QuoteName(alias));
          outSql = new CompositeSqlFragment(outSql, SqlTerms.As, aliasPart);
        }
        outCols.Add(outSql);
      }
      var outColsPart = SqlFragment.CreateList(SqlTerms.Comma, outCols);
      return outColsPart; 
    }


    public virtual SqlFragment BuildFromClause(SelectExpression selectExpression, IList<TableExpression> tables) {
      if(tables.Count == 0)
        return null; //this can happen: 'SELECT 1'
      var parts = new List<SqlFragment>();
      parts.Add(SqlTerms.From); 

      var table0Sql = BuildTableForFrom(tables[0]);
      parts.Add(table0Sql);
      var hasNonInnerJoins = tables.Any(t => t.JoinType != TableJoinType.Inner);
      foreach(var table in tables.Skip(1)) { //do not join first table
        var tableRefSql = BuildTableForFrom(table);
        if(table.JoinExpression == null) {
          parts.Add(SqlTerms.Comma);
          parts.Add(tableRefSql);
          continue;
        } else {
          // get constitutive Parts
          var joinCondSql = BuildLinqExpressionSql(table.JoinExpression);
          var joinSql = BuildTableJoin(table.JoinType, tableRefSql, joinCondSql);
          parts.Add(joinSql);
        }
      }
      return new CompositeSqlFragment(parts);
    }

    protected virtual SqlFragment BuildTableJoin(TableJoinType joinType, SqlFragment tableRef, SqlFragment joinCond) {
      SqlFragment joinDecl = null; 
      switch(joinType) {
        case TableJoinType.Inner: joinDecl = SqlTerms.InnerJoin; break;
        case TableJoinType.LeftOuter: joinDecl = SqlTerms.LeftJoin; break;
        case TableJoinType.RightOuter: joinDecl = SqlTerms.RightJoin; break; 
        default:
          Util.Check(false, "Join type {0} not supported.", joinType);
          break; 
      }
      return new CompositeSqlFragment(SqlTerms.NewLine, SqlTerms.Indent, joinDecl, tableRef, SqlTerms.On, joinCond);
    }

    public virtual SqlFragment BuildTableForFrom(TableExpression te) {
      SqlFragment result; 
      if (te is SubSelectExpression) {
        result = BuildSqlForSqlExpression(te);
      } else 
        result = new TextSqlFragment(te.TableInfo.FullName);
      if(te.HasAlias())
        result = new CompositeSqlFragment(result, SqlTerms.Space, te.GetAliasSql());
      return result;
    }

    public virtual SqlFragment BuildWhereClause(SelectExpression selectExpression, IList<Expression> wheres) {
      if(wheres.Count == 0)
        return null;
      var whereParts = new List<SqlFragment>();
      foreach(var whereExpression in wheres) 
          whereParts.Add(BuildLinqExpressionSql(whereExpression));
      var whereAll = whereParts.Count == 1 ? whereParts[0] : SqlFragment.CreateList(SqlTerms.And, whereParts);
      return new CompositeSqlFragment(SqlTerms.Where, whereAll);
    }

    public virtual SqlFragment BuildHavingClause(SelectExpression selectExpression) {
      var exprs = selectExpression.Having; 
      if(exprs.Count == 0)
        return null;
      var parts = new List<SqlFragment>();
      foreach(var expr in exprs)
        parts.Add(BuildLinqExpressionSql(expr));
      //TODO: rework this with explicit merge of exprs using AND expr
      var all = SqlFragment.CreateList(SqlTerms.And, parts);
      return new CompositeSqlFragment(SqlTerms.Having, all);
    }

    public virtual SqlFragment BuildGroupByClause(SelectExpression selectExpression) {
      var exprs = selectExpression.Group; 
      if(exprs.Count == 0)
        return null;
      var parts = new List<SqlFragment>();
      foreach(var ge in exprs)
        foreach(var c in ge.Columns)
          parts.Add(BuildLinqExpressionSql(c));
      // this might happen with fake grouping to get aggregates on entire table
      if(parts.Count == 0)
        return null; 
      var gbList = SqlFragment.CreateList(SqlTerms.Comma, parts);
      return new CompositeSqlFragment(SqlTerms.GroupBy, gbList);
    }

    public virtual SqlFragment BuildOrderByClause(SelectExpression select) {
      //Special case - fake OrderBy clause 'ORDER BY (SELECT 1)', used in SQLs with Take/skip in MS SQL, Postgres
      if(select.Flags.IsSet(SelectExpressionFlags.NeedsFakeOrderBy))
        return this.SqlDialect.SqlFakeOrderByClause;
      if(select.OrderBy.Count == 0)
        return null;
      var parts = new List<SqlFragment>();
      foreach(var e in select.OrderBy)
        parts.Add(BuildLinqExpressionSql(e));
      var all = SqlFragment.CreateList(SqlTerms.Comma, parts);
      return this.SqlDialect.SqlTemplateOrderBy.Format(all);
    }

    public virtual SqlFragment BuildLimitClause(SelectExpression selectExpression) {
      var limit = selectExpression.Limit;
      var offset = selectExpression.Offset; 
      if(limit == null && offset == null)
        return null;
      //var limit
      var sqlLimit = limit == null ? null : BuildLinqExpressionSql(limit);
      var sqlOffset = offset == null ? null : BuildLinqExpressionSql(offset);
      var limitOffset = BuildLimitSql(sqlLimit, sqlOffset);
      return limitOffset;
    }

    public string GetAlias(Expression expr) {
      switch(expr) {
        case SqlExpression se:
          return se.Alias;
        default:
          return null;
      }
    }

    // should be overriden
    protected virtual SqlFragment BuildLimitSql(SqlFragment limit, SqlFragment offset) {
      return null; 
    }

    public virtual SqlFragment BuildLockClause(SelectExpression selectExpression, LockType lockType) {
      return null; 
    }




  }
}
