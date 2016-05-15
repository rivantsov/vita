
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities;
using Vita.Entities.Linq;

namespace Vita.Data.Linq.Translation.SqlGen {

    internal partial class SqlBuilder 
    {
        private Vita.Data.Model.DbModel _dbModel;
        private Vita.Data.Driver.LinqSqlProvider _sqlProvider;

        public SqlBuilder(Vita.Data.Model.DbModel dbModel) {
          _dbModel = dbModel;
          _sqlProvider = _dbModel.LinqSqlProvider;
        }

        public SqlStatement BuildSelect(SelectExpression selectExpression) {
          var stmt = BuildSelectSql(selectExpression);
          stmt = _sqlProvider.ReviewSelectSql(selectExpression, stmt);
          return stmt; 
        }

        private SqlStatement BuildSelectSql(SelectExpression selectExpression) {
            selectExpression = _sqlProvider.PreviewSelect(selectExpression);

            // A scope usually has:
            // - a SELECT: the operation creating a CLR object with data coming from SQL tier
            // - a FROM: list of tables
            // - a WHERE: list of conditions
            // - a GROUP BY: grouping by selected columns
            // - a ORDER BY: sort
            var select = BuildSelectClause(selectExpression);
            if (select.ToString() == string.Empty)
            {
                SubSelectExpression subselect = null;
                if (selectExpression.Tables.Count == 1)
                    subselect = selectExpression.Tables[0] as SubSelectExpression;
                if(subselect != null)
                    return _sqlProvider.GetParenthesis(BuildSelectSql(subselect.Select));
            }

            // TODO: the following might be wrong (at least this might be the wrong place to do this
            if (select.ToString() == string.Empty)
                select = new SqlStatement("SELECT " + _sqlProvider.GetLiteral(null) + " AS " + _sqlProvider.GetSafeName("Empty"));

            var tables = GetSortedTables(selectExpression);
            var from = BuildFrom(tables);
            var join = BuildJoin(tables);
            var where = BuildWhere(tables, selectExpression.Where);
            var groupBy = BuildGroupBy(selectExpression.Group);
            var having = BuildHaving(selectExpression.Where);
            var orderBy = BuildOrderBy(selectExpression.OrderBy);
            select = Join(select, from, join, where, groupBy, having, orderBy);
            select = BuildLimit(selectExpression, select);

            if (selectExpression.NextSelectExpression != null)
            {
                var nextLiteralSelect = BuildSelectSql(selectExpression.NextSelectExpression);
                select = _sqlProvider.GetLiteral(
                    selectExpression.NextSelectExpressionOperator,
                    select, nextLiteralSelect);
            }
            return select;
        }

        protected virtual SqlStatement BuildSelectClause(Expression select) {
          var selectClauses = new List<SqlStatement>();
          var ops = select.GetOperands().ToList();
          foreach(var outputExpr in ops) {
            var exprString = BuildOutputExpression(outputExpr);
            selectClauses.Add(exprString);
          }
          SelectExpression selectExp = select as SelectExpression;
          if(selectExp != null) {
            if(selectExp.Group.Count == 1 && selectExp.Group[0].IsDistinct) {
              //RI: changed code
              var g = selectExp.Group[0];
              var outCols = new List<SqlStatement>();
              foreach(var col in g.Columns) {
                var sqlCol = col.Table.Alias == null ?
                    _sqlProvider.GetColumn(col.Name) :
                    _sqlProvider.GetColumn(_sqlProvider.GetTableAlias(col.Table.Alias), col.Name);
                if(!string.IsNullOrEmpty(col.Alias))
                  sqlCol += " AS " + col.Alias;
                outCols.Add(sqlCol);
              }
              return _sqlProvider.GetSelectDistinctClause(outCols.ToArray());
              /*
              // this is a select DISTINCT expression
                // TODO: better handle selected columns on DISTINCT: I suspect this will not work in some cases
                if (selectClauses.Count == 0)
                {
                    selectClauses.Add(_sqlProvider.GetColumns());
                }
                return _sqlProvider.GetSelectDistinctClause(selectClauses.ToArray());
               */
            }
          }
          return _sqlProvider.GetSelectClause(selectClauses.ToArray());
        }

        //Builds select output column/expression
        protected string BuildOutputExpression(Expression outputExpr) {
          var exprString = BuildExpression(outputExpr).ToString();
          if(outputExpr is SelectExpression)
            exprString = Environment.NewLine + "  " + _sqlProvider.GetParenthesis(exprString); //put it on a separate line
          if(outputExpr is SqlExpression) {
            var sqlExpr = (SqlExpression)outputExpr;
            if(!string.IsNullOrWhiteSpace(sqlExpr.Alias))
              exprString += " AS \"" + sqlExpr.Alias + "\"";
          }
          if (exprString.Contains(Environment.NewLine))
            exprString += Environment.NewLine; //add new line on the end
          return exprString;
        }

        /// <summary>
        /// Returns a list of sorted tables, given a select expression.
        /// The tables are sorted by dependency: independent tables first, dependent tables next
        /// </summary>
        /// <param name="selectExpression"></param>
        /// <returns></returns>
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

        public SqlStatement Join(params SqlStatement[] clauses)
        {
            return SqlStatement.Join(_sqlProvider.NewLine,
                               (from clause in clauses where clause.ToString() != string.Empty select clause).ToList());
        }

        protected virtual SqlStatement BuildExpression(Expression expression)
        {

            if (expression is ConstantExpression)
              return _sqlProvider.GetLiteral(((ConstantExpression)expression).Value);

            var currentPrecedence = ExpressionUtil.GetPrecedence(expression);
            // first convert operands
            var operands = expression.GetOperands();
            var literalOperands = new List<SqlStatement>();
            foreach (var operand in operands)
            {
                var operandPrecedence = ExpressionUtil.GetPrecedence(operand);
                var literalOperand = BuildExpression(operand);
                if (operandPrecedence > currentPrecedence)
                    literalOperand = _sqlProvider.GetParenthesis(literalOperand);
                literalOperands.Add(literalOperand);
            }

            if (expression is AliasedExpression) {
              var aliasExpr = (AliasedExpression) expression;
              return BuildExpression(aliasExpr.Expression); //Alias will be added later
            }
            // then converts expression
            if (expression is SqlFunctionExpression) {
              var specExpr = (SqlFunctionExpression)expression;
              //RI: Special case for multiple "*" operands
              if (specExpr.FunctionType == SqlFunctionType.Count && literalOperands.Count > 0) {
                literalOperands.Clear();
                literalOperands.Add("*");              
              }
              return _sqlProvider.GetSqlFunction(specExpr.FunctionType, specExpr.ForceIgnoreCase, literalOperands);
            }
            if (expression is TableExpression)
            {
                var tableExpression = (TableExpression)expression;
                if (tableExpression.Alias != null) // if we have an alias, use it
                {
                    return _sqlProvider.GetColumn(_sqlProvider.GetTableAlias(tableExpression.Alias),
                                                 _sqlProvider.GetColumns());
                }
                return _sqlProvider.GetColumns();
            }

            //RI: We might have NewExpression here! Query: (from b in books select new {b.Title}).Count();
            // in this case the NewExpression is 'hidden' inside subquery and it is not visible to CutOutOperands
            // We just return list of arguments (columns) of New expression
            if (expression is NewExpression) 
              return new SqlStatement(literalOperands); 
            //RI: adding this
            if (expression is MetaTableExpression) {
              var metaTable = (MetaTableExpression)expression;
              return _sqlProvider.GetColumns(); 
            }

            if (expression is ColumnExpression)
            {
                var columnExpression = (ColumnExpression)expression;
                if(columnExpression.Table.Alias != null)
                {
                    return _sqlProvider.GetColumn(_sqlProvider.GetTableAlias(columnExpression.Table.Alias),
                                                 columnExpression.Name);
                }
                //RI: changed this to keep output type
                var sqlPart = new SqlLiteralPart(_sqlProvider.GetColumn(columnExpression.Name), expression.Type);
                return new SqlStatement(sqlPart);
                //return _sqlProvider.GetColumn(columnExpression.Name);
            }

            if (expression is ExternalValueExpression)  {
                var extValue = (ExternalValueExpression)expression;
                
                switch(extValue.SqlUse) {
                  case ExternalValueSqlUse.Literal:
                    var sql = _sqlProvider.GetLiteral(extValue.LiteralValue);
                    return sql; 
                  case ExternalValueSqlUse.Parameter:
                    // In SQL templates the first 2 args are reserved for { and } symbols
                    return _sqlProvider.GetParameter(extValue);
                  default:
                    // we should never get here
                    Util.Throw("LINQ engine error: encountered ExternalValueExpression with invalid Usage type: {0}, Expression: {1}",
                      extValue.SqlUse, extValue.SourceExpression);
                    return null; //never happens 
                } 
            }
            if (expression is SelectExpression)
                return BuildSelectSql((SelectExpression)expression);
            if (expression is GroupExpression)
                return BuildExpression(((GroupExpression)expression).GroupedExpression);

            StartIndexOffsetExpression indexExpression = expression as StartIndexOffsetExpression;
            if (indexExpression!=null)
            {
                if (indexExpression.StartsAtOne)
                {
                    literalOperands.Add(BuildExpression(Expression.Constant(1)));
                    return _sqlProvider.GetLiteral(ExpressionType.Add, literalOperands);
                }
                else
                    return literalOperands.First();
            }
            if (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                var unaryExpression = (UnaryExpression)expression;
                var firstOperand = literalOperands.First();
                if (IsConversionRequired(unaryExpression))
                    return _sqlProvider.GetLiteralConvert(firstOperand, unaryExpression.Type);
                return firstOperand;
            }
            if (expression is BinaryExpression || expression is UnaryExpression)
              return _sqlProvider.GetLiteral(expression.NodeType, literalOperands);

            if (expression is TableFilterExpression)
              return _sqlProvider.GetTableFilter((TableFilterExpression)expression);

            return _sqlProvider.GetLiteral(expression.NodeType, literalOperands);
        }

        /// <summary>
        /// Determines if a SQL conversion is required
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        private bool IsConversionRequired(UnaryExpression expression)
        {
            // obvious (and probably never happens), conversion to the same type
            if (expression.Type == expression.Operand.Type)
                return false;
            if (IsEnumAndInt(expression.Type, expression.Operand.Type))
              return false;
            //RI: trying to prevent CONVERT in order by clause
            if(expression.Type == typeof(object))
              return false; 
            //RI: special case - do not convert bit column expression to int - they are already ints
            if (expression.Type == typeof(int) && expression.Operand is ColumnExpression && expression.Operand.Type == typeof(bool))
              return false; 
            // second, nullable to non-nullable for the same type
            if (expression.Type.IsNullableValueType() && !expression.Operand.Type.IsNullableValueType())
            {
                if (expression.Type.GetUnderlyingType() == expression.Operand.Type)
                    return false;
            }
            // third, non-nullable to nullable
            if (!expression.Type.IsNullableValueType() && expression.Operand.Type.IsNullableValueType())
            {
                if (expression.Type == expression.Operand.Type.GetUnderlyingType())
                    return false;
            }
            // found no excuse not to convert? then convert
            return true;
        }

        private bool IsEnumAndInt(Type x, Type y) {
          return x.IsEnum && Enum.GetUnderlyingType(x) == y || y.IsEnum && Enum.GetUnderlyingType(y) == x;
        }

        protected virtual bool MustDeclareAsJoin(IList<TableExpression> tables, TableExpression table)
        {
          // Temp hack, trying make all joins
          if(tables.Count == 1 && table == tables[0])
            return false;
          if(table.JoinExpression == null)
            return false; 
          return true; 
/*
            // the first table can not be declared as join
            if (table == tables[0])
                return false;
            // we must declare as join, whatever the join is,
            // if some of the registered tables are registered as complex join
            if (tables.Any(t => t.JoinType != TableJoinType.Inner))
                return table.JoinExpression != null;
            return false;
 */ 
        }

        protected virtual SqlStatement BuildFrom(IList<TableExpression> tables)
        {
          var sqlProvider = this._dbModel.LinqSqlProvider;
            var fromClauses = new List<SqlStatement>();
            foreach (var tableExpression in tables)
            {
                if (!MustDeclareAsJoin(tables, tableExpression))
                {
                    if (tableExpression.Alias != null)
                    {
                        string tableRef;

                        // All subqueries has an alias in FROM
                        SubSelectExpression subquery = tableExpression as SubSelectExpression;
                        if (subquery == null)
                            tableRef = _sqlProvider.GetTable(tableExpression);
                        else
                        {
                            var subqueryStatements = new SqlStatement(BuildSelectSql(subquery.Select));
                            tableRef = _sqlProvider.GetSubQueryAsAlias(subqueryStatements.ToString(), tableExpression.Alias);
                        }

                        if ((tableExpression.JoinType & TableJoinType.LeftOuter) != 0)
                            tableRef = "/* LEFT OUTER */ " + tableRef;
                        if ((tableExpression.JoinType & TableJoinType.RightOuter) != 0)
                            tableRef = "/* RIGHT OUTER */ " + tableRef;
                        fromClauses.Add(tableRef);
                    }
                    else
                    {
                        fromClauses.Add(_sqlProvider.GetTable(tableExpression));
                    }
                }
            }
            return _sqlProvider.GetFromClause(fromClauses.ToArray());
        }

        protected virtual SqlStatement BuildJoin(IList<TableExpression> tables)
        {
            var sqlProvider = _dbModel.LinqSqlProvider;
            var joinClauses = new List<SqlStatement>();
            foreach (var tableExpression in tables)
            {
                // this is the pending declaration of direct tables
                if (MustDeclareAsJoin(tables, tableExpression))
                {
                    // get constitutive Parts
                    var joinExpression = BuildExpression(tableExpression.JoinExpression);
                    var tableRef = _sqlProvider.GetTable(tableExpression);
                    SqlStatement joinClause;
                    switch (tableExpression.JoinType)
                    {
                        case TableJoinType.Inner:
                            joinClause = _sqlProvider.GetInnerJoinClause(tableRef, joinExpression);
                            break;
                        case TableJoinType.LeftOuter:
                            joinClause = _sqlProvider.GetLeftOuterJoinClause(tableRef, joinExpression);
                            break;
                        case TableJoinType.RightOuter:
                            joinClause = _sqlProvider.GetRightOuterJoinClause(tableRef, joinExpression);
                            break;
                        case TableJoinType.FullOuter:
                            throw new NotImplementedException();
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    joinClauses.Add(joinClause);
                }
            }
            return _sqlProvider.GetJoinClauses(joinClauses.ToArray());
        }

        protected virtual bool IsHavingClause(Expression expression)
        {
            bool isHaving = false;
            expression.Recurse(delegate(Expression e)
                                   {
                                       if (e is GroupExpression)
                                           isHaving = true;
                                       return e;
                                   });
            return isHaving;
        }

        protected virtual SqlStatement BuildWhere(IList<TableExpression> tables, IList<Expression> wheres)
        {
            var sqlProvider = _dbModel.LinqSqlProvider;
            var whereClauses = new List<SqlStatement>();
            foreach (var tableExpression in tables)
            {
                if (!MustDeclareAsJoin(tables, tableExpression) && tableExpression.JoinExpression != null)
                    whereClauses.Add(BuildExpression(tableExpression.JoinExpression));
            }
            foreach (var whereExpression in wheres)
            {
                if (!IsHavingClause(whereExpression))
                    whereClauses.Add(BuildExpression(whereExpression));
            }
            return _sqlProvider.GetWhereClause(whereClauses.ToArray());
        }

        protected virtual SqlStatement BuildHaving(IList<Expression> wheres)
        {
            var havingClauses = new List<SqlStatement>();
            foreach (var whereExpression in wheres)
            {
                if (IsHavingClause(whereExpression))
                    havingClauses.Add(BuildExpression(whereExpression));
            }
            return _sqlProvider.GetHavingClause(havingClauses.ToArray());
        }

        protected virtual SqlStatement GetGroupByClause(ColumnExpression columnExpression)
        {
            if (columnExpression.Table.Alias != null)
            {
                return _sqlProvider.GetColumn(_sqlProvider.GetTableAlias(columnExpression.Table.Alias),
                                             columnExpression.Name);
            }
            return _sqlProvider.GetColumn(columnExpression.Name);
        }

        protected virtual SqlStatement BuildGroupBy(IList<GroupExpression> groupByExpressions)
        {
            var groupByClauses = new List<SqlStatement>();
            foreach (var groupByExpression in groupByExpressions)
            {
                if (groupByExpression.IsDistinct) continue; //RI: added this
                foreach (var operand in groupByExpression.Columns)
                {
                    var columnOperand = operand as ColumnExpression;
                    if (columnOperand == null)
                        Util.Throw("S0201: Groupby argument must be a ColumnExpression");
                    groupByClauses.Add(GetGroupByClause(columnOperand));
                }
            }
            return _sqlProvider.GetGroupByClause(groupByClauses.ToArray());
        }

        protected virtual SqlStatement BuildOrderBy(IList<OrderByExpression> orderByExpressions)
        {
          //Special case - fake OrderBy clause 'ORDER BY (SELECT 1)', used in SQLs with Take/skip in MS SQL, Postgres
          if(orderByExpressions.Count == 1 && orderByExpressions[0].ColumnExpression == null)
            return _sqlProvider.GetFakeOrderByClause();
          var orderByClauses = new List<SqlStatement>();
          foreach(var orderBy in orderByExpressions)
          {
              orderByClauses.Add(_sqlProvider.GetOrderByColumn(BuildExpression(orderBy.ColumnExpression),
                                                              orderBy.Descending));
          }
            return _sqlProvider.GetOrderByClause(orderByClauses.ToArray());
        }

        protected virtual SqlStatement BuildLimit(SelectExpression select, SqlStatement literalSelect)
        {
            if (select.Limit == null)
              return literalSelect;
            var literalLimit = BuildExpression(select.Limit);
            var literalOffset = BuildExpression(select.Offset);
            var literalOffsetAndLimit = BuildExpression(select.OffsetAndLimit);
            return _sqlProvider.GetLiteralLimit(literalSelect, literalLimit, literalOffset, literalOffsetAndLimit);
        }

    }
}