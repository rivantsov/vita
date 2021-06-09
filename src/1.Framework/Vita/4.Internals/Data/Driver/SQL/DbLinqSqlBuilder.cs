using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using System.Linq.Expressions;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  public partial class DbLinqSqlBuilder {

    protected DbModel DbModel;
    protected DbSqlDialect SqlDialect;
    protected LinqCommand Command;
    protected SqlPlaceHolderList PlaceHolders = new SqlPlaceHolderList();


    public DbLinqSqlBuilder(DbModel dbModel, LinqCommand command) {
      DbModel = dbModel;
      SqlDialect = DbModel.Driver.SqlDialect;
      Command = command;
    }

    public virtual SqlStatement BuildSelectStatement(SelectExpression translatedSelect) {
      var sql = BuildSelectSql(translatedSelect); 
      var sqlStmt = new SqlStatement(SqlKind.LinqSelect, sql, PlaceHolders, DbExecutionType.Reader,
                       DbModel.Driver.SqlDialect.PrecedenceHandler, translatedSelect.Command.Options);
      sqlStmt.Fragments.Add(SqlTerms.Semicolon);
      sqlStmt.Fragments.Add(SqlTerms.NewLine);
      return sqlStmt;
    }

    public virtual SqlFragment BuildSelectSql(SelectExpression select) {
      var sql = BuildSelectSqlSimple(select);
      if (select.ChainedSet == null)
        return sql; 
      // build multi-set SQL (UNION of multiple)
      var sqls = new List<SqlFragment>();
      sqls.Add(sql);
      var nextSet = select.ChainedSet; 
      while(nextSet != null) {
        sqls.Add(SqlDialect.GetMultiSetOperator(nextSet.MultiSetType));
        sqls.Add(BuildSelectSqlSimple(nextSet.ChainedSelect));
        nextSet = nextSet.ChainedSelect.ChainedSet; 
      }
      var resultSql = SqlFragment.CreateList(SqlTerms.NewLine, sqls);
      return resultSql;
    }

    protected virtual SqlFragment BuildSelectSqlSimple(SelectExpression select) {
      var lockType = select.Command.LockType;
      select = PreviewSelect(select, lockType);
      var selectOut = BuildSelectOutputClause(select);
      var tables = GetSortedTables(select);
      var from = BuildFromClause(select, tables);
      var where = BuildWhereClause(select, select.Where);
      var groupBy = BuildGroupByClause(select);
      var having = BuildHavingClause(select);
      var orderBy = BuildOrderByClause(select);
      var lockClause = BuildLockClause(select, lockType);
      var limit = BuildLimitClause(select);
      var all = (new SqlFragment[] { selectOut, from, where, groupBy, having, orderBy, lockClause, limit})
        .Where(f => f != null).ToList();
      var sql = SqlFragment.CreateList(SqlTerms.NewLine, all);
      return sql; 
    }

    public virtual SelectExpression PreviewSelect(SelectExpression select, LockType lockType) {
      //None of the servers support in one query COUNT and Limit (FETCH NEXT for MS SQL) 
      if(select.HasLimit() && select.HasOutAggregates())
        Util.Throw("Invalid LINQ expression: Server does not support COUNT(*) and LIMIT (MS SQL: FETCH NEXT) in one query.");
      if (select.Group.Count == 1 && select.Group[0].IsDistinct)
        select.Flags |= SelectExpressionFlags.Distinct; 
      return select;
    }

    //TODO: rewrite or get rid of it!
    protected IList<TableExpression> GetSortedTables(SelectExpression selectExpression) {
      //RI: I have rewritten this
      if(selectExpression.Tables.Count < 2)
        return selectExpression.Tables;
      var tables = new List<TableExpression>(selectExpression.Tables);
      foreach(var table in tables)
        table.SortIndex = 0;
      for(int i = 0; i < tables.Count * 2; i++) {
        bool updated = false;
        //make one round of updates
        foreach(var t in tables)
          if(t.JoinedTable != null && t.JoinedTable.SortIndex <= t.SortIndex) {
            t.JoinedTable.SortIndex = t.SortIndex + 1;
            updated = true;
          }
        if(!updated) {
          tables.Sort((x, y) => y.SortIndex.CompareTo(x.SortIndex));
          return tables;
        }
      } //for i
      Util.Throw("Internal LINQ engine error - failed to sort joined tables.");
      return tables;
    }


    public virtual SqlFragment BuildLinqExpressionSql(Expression expr) {
      if(expr.Type != null && expr.Type.IsSubclassOf(typeof(LinqLiteralBase)))
        return BuildSqlForSqlLiteral(expr);
      if(expr.NodeType == ExpressionType.Extension)
        return BuildSqlForSqlExpression((SqlExpression)expr); 
      else 
        return BuildSqlForStandardExpression(expr);
    }

    public virtual SqlFragment BuildSqlForSqlLiteral(Expression expr) {
      var value = ExpressionHelper.Evaluate(expr);
      var literal = (LinqLiteralBase)value;
      return literal.GetSql(this.DbModel); 

    }

    public virtual SqlFragment BuildSqlForStandardExpression(Expression expr) {
      var args = expr.GetOperands();
      var sqlArgs = args.Select(a => BuildLinqExpressionSql(a)).ToArray();
      switch(expr) {
        case ConstantExpression ce:
          return GetConstantLiteral(ce.Value, ce.Type);
        case NewExpression ne:
          //RI: We might have NewExpression here! Query: (from b in books select new {b.Title}).Count();
          // in this case the NewExpression is 'hidden' inside subquery and it is not visible to CutOutOperands
          // We just return list of arguments (columns) of New expression
          return SqlFragment.CreateList(SqlTerms.Comma, sqlArgs);
        case UnaryExpression ue: 
          if (ue.NodeType == ExpressionType.Convert || ue.NodeType == ExpressionType.ConvertChecked) {
            if (this.SqlDialect.IsConversionRequired(ue))
              return BuildConvertSql(ue);
            else
              return BuildLinqExpressionSql(ue.Operand); //no conversion
          }
          break; 
      }
      //Binary, unary expr based on templates
      var template = SqlDialect.GetExpressionTemplate(expr); 
      if (template != null) 
        return template.Format(sqlArgs); 
      Util.Throw("Not supported expression, cannot convert to SQL, expr type {0}, expr: {1} ", expr.NodeType, expr);
      return null; 
    }//method


    public virtual SqlFragment BuildSqlForSqlExpression(SqlExpression expr) {
      switch(expr.SqlNodeType) {
        case SqlExpressionType.Alias:
          var aliasExpr = (AliasedExpression)expr;
          return BuildLinqExpressionSql(aliasExpr.Expression); //Alias will be added later
        case SqlExpressionType.SqlFunction:
          return BuildSqlForSqlFunctionExpression((SqlFunctionExpression)expr);
        case SqlExpressionType.Column:
          var colExpr = (ColumnExpression)expr;
          return GetColumnRefSql(colExpr, forOutput: false);

        case SqlExpressionType.SubSelect:
          var subs = (SubSelectExpression)expr;
          var subSelect = BuildSqlForSqlExpression(subs.Select);
          return CompositeSqlFragment.Parenthesize(subSelect);  
            
        case SqlExpressionType.Table:
          var te = (TableExpression)expr;
          return te.TableInfo.SqlFullName; 

        case SqlExpressionType.TableFilter:
          var tfe = (TableFilterExpression)expr;
          var argParts = BuildSqls(tfe.Columns);
          var sqlTempl = new SqlTemplate(tfe.Filter.EntityFilter.Template.StandardForm);
          var part = sqlTempl.Format(argParts);
          return part;

        case SqlExpressionType.ExternalValue:
          var extValue = (ExternalValueExpression)expr;
          var ph = CreateSqlPlaceHolder(extValue); 
          // it is already added to placeholders list
          return ph;

        case SqlExpressionType.Group:
          var ge = (GroupExpression)expr;
          return BuildLinqExpressionSql(ge.GroupedExpression);
         
        case SqlExpressionType.OrderBy:
          return BuildOrderByMember((OrderByExpression)expr);

        case SqlExpressionType.Aggregate:
          return BuildAggregateSql((AggregateExpression) expr); 

        case SqlExpressionType.Select:
          var selectSql = BuildSelectSql((SelectExpression)expr);
          return CompositeSqlFragment.Parenthesize(selectSql);

        case SqlExpressionType.CustomSqlSnippet:
          return BuildCustomSqlFromSnippet( (CustomSqlExpression)expr);

        case SqlExpressionType.DerivedTable:
          // Looks like we never come here
          //!!! investigate this
          //TODO: investigate DerivedTable SQL
          return SqlTerms.Star;

        default:
          Util.Throw("SqlExpression->SQL not implemented, SqlNodeType: {0}", expr.SqlNodeType);
          return null; //never happens
      }//switch
      
    }//method


    private SqlFragment BuildCustomSqlFromSnippet(CustomSqlExpression custSqlExpr) {
      var prmsOrder = custSqlExpr.Snippet.ParamsReorder;
      var orderedArgs = new List<Expression>();
      for (int i = 0; i < custSqlExpr.Operands.Count; i++) {
        orderedArgs.Add(custSqlExpr.Operands[prmsOrder[i]]);
      }
      var argSqls = BuildSqls(orderedArgs);
      var sqlTempl = custSqlExpr.Snippet.Template;
      var sqlFragm = sqlTempl.Format(argSqls);
      return sqlFragm;

    }

    public virtual SqlFragment BuildOrderByMember(OrderByExpression obExpr) {
      var colPart = BuildLinqExpressionSql(obExpr.ColumnExpression);
      if (obExpr.Descending)
        return new CompositeSqlFragment(colPart, SqlTerms.Desc);
      else
        return colPart;

    }

    public SqlFragment[] BuildSqls<TExpr>(IList<TExpr> expressions) where TExpr: Expression {
      return expressions.Select(e => BuildLinqExpressionSql(e)).ToArray(); 
    }

    public virtual SqlFragment BuildSqlForSqlFunctionExpression(SqlFunctionExpression expr) {
      SqlTemplate template = SqlDialect.GetSqlFunctionTemplate(expr);
      if (template != null) {
        var args = expr.GetOperands();
        var sqlArgs = args.Select(a => BuildLinqExpressionSql(a)).ToArray();
        return template.Format(sqlArgs);
      }
      return BuildSqlForSqlFunctionExpressionNoTemplate(expr);  
    }//class

    public virtual SqlFragment BuildSqlForSqlFunctionExpressionNoTemplate(SqlFunctionExpression expr) {
      //concat - special case; turn all args into comma-delimited list
      if(expr.FunctionType == SqlFunctionType.Concat) {
        var args = expr.GetOperands();
        var sqlArgs = args.Select(a => BuildLinqExpressionSql(a)).ToList();
        var argsPart = SqlFragment.CreateList(SqlDialect.SqlConcatListDelimiter, sqlArgs);
        return SqlDialect.SqlTemplateConcatMany.Format(argsPart);
      }
      Util.Throw("Unsupported SqlFunction type: {0}, expr: {1} ", expr.FunctionType, expr);
      return null; 
    }


    public virtual SqlFragment BuildAggregateSql(AggregateExpression expr) {
      if(expr.AggregateType == AggregateType.Count && expr.Operands.Count == 0) 
         return this.SqlDialect.SqlCountStar;
      SqlTemplate template = SqlDialect.GetAggregateTemplate(expr);
      if (template != null) {
        var args = expr.GetOperands();
        var sqlArgs = args.Select(a => BuildLinqExpressionSql(a)).ToArray();
        return template.Format(sqlArgs);
      }
      Util.Throw("Unsupported Aggregate type: {0}, expr: {1} ", expr.AggregateType, expr);
      return null;
    }

    public virtual SqlFragment BuildConvertSql(UnaryExpression ue) {
      // this is default in base class, no conversion
      return BuildLinqExpressionSql(ue.Operand); 
    }

    public virtual SqlFragment GetColumnRefSql(ColumnExpression column, bool forOutput) {
      var colPart = new TextSqlFragment(column.ColumnInfo.ColumnNameQuoted);
      var tbl = column.Table;
      SqlFragment result = colPart; 
      // finish this with col alias
      if(tbl.HasAlias())
        result = new CompositeSqlFragment(tbl.GetAliasSql(), SqlTerms.Dot, colPart);
      if(forOutput && column.HasAlias()) {
        var colAlias = new TextSqlFragment(column.Alias);
        result = new CompositeSqlFragment(result, colAlias);
      }
      return result;
    }

    public virtual SqlFragment GetConstantLiteral(object value, Type type) {
      if(value == null)
        return SqlTerms.Null;
      if (type == typeof(SequenceDefinition)) {
        // TODO: see if it ever happens
        //Util.Throw("Investigate: literal for sequence value");
        var seq = (SequenceDefinition)value;
        var dbSeq = this.DbModel.GetSequence(seq);
        return new TextSqlFragment(dbSeq.FullName);
      }
      var stype = this.DbModel.Driver.TypeRegistry.GetDbTypeDef(type);
      var literal = stype.ToLiteral(value);
      return new TextSqlFragment(literal); 
    }


  }
}
