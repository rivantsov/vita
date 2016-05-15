
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using System.Linq.Expressions;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Linq;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// ScopeExpression describes a selection.
    /// It can be present at top-level or as subexpressions
    /// </summary>
    public class SelectExpression : OperandsMutableSqlExpression
    {
        public LinqCommandInfo CommandInfo;
        // Involved entities
        public IList<TableExpression> Tables { get; private set; }
        public IList<ColumnExpression> Columns { get; private set; }

        // Clauses
        public QueryResultsProcessor ResultsProcessor { get; set; }

        public LambdaExpression Reader { get; set; } // Func<IDataRecord,EntitySession,T> --> creates an object from data record
        public IList<Expression> Where { get; private set; }
        public IList<OrderByExpression> OrderBy { get; private set; }
        public IList<GroupExpression> Group { get; private set; }

        public Expression Offset { get; set; }
        public Expression Limit { get; set; }
        public Expression OffsetAndLimit { get; set; }

        // the following two clauses are used by expressions of same level, linked by a special operation (like "union")
        public SelectExpression NextSelectExpression;
        public SelectOperatorType NextSelectExpressionOperator;

        // Parent scope: we will climb up to find if we don't find the request table in the current scope
        public SelectExpression Parent { get; set; }

        public SelectExpression()   : base(SqlExpressionType.Select, null, null)  {
            Tables = new List<TableExpression>();
            Columns = new List<ColumnExpression>();
            // Local clauses
            Where = new List<Expression>();
            OrderBy = new List<OrderByExpression>();
            Group = new List<GroupExpression>();
        }

        public SelectExpression(SelectExpression parentSelectExpression) : base(SqlExpressionType.Select, null, null)
        {
            Parent = parentSelectExpression;
            // Tables and columns are empty, since the table/column lookup recurses to parentScopePiece
            Tables = new List<TableExpression>();
            Columns = new List<ColumnExpression>();
            // Local clauses
            Where = new List<Expression>();
            OrderBy = new List<OrderByExpression>();
            Group = new List<GroupExpression>();
        }

        private SelectExpression(Type type, IList<Expression> operands) : base(SqlExpressionType.Select, type, operands)
        {
        }

        protected override Expression Mutate2(IList<Expression> newOperands)
        {
            Type type;
            if (newOperands.Count > 0)
                type = newOperands[0].Type;
            else
                type = Type;
            var scopeExpression = new SelectExpression(type, newOperands);
            scopeExpression.Tables = Tables;
            scopeExpression.Columns = Columns;
            scopeExpression.Where = Where;
            scopeExpression.OrderBy = OrderBy;
            scopeExpression.Group = Group;
            scopeExpression.Parent = Parent;
            scopeExpression.ResultsProcessor = ResultsProcessor;
            scopeExpression.Reader = Reader;
            scopeExpression.Limit = Limit;
            scopeExpression.Offset = Offset;
            scopeExpression.OffsetAndLimit = OffsetAndLimit;
            scopeExpression.NextSelectExpression = NextSelectExpression;
            scopeExpression.NextSelectExpressionOperator = NextSelectExpressionOperator;
            return scopeExpression;
        }

        //helper methods
        static SqlFunctionType[] _aggregates = new [] {
            SqlFunctionType.Count, SqlFunctionType.Min, SqlFunctionType.Max, SqlFunctionType.Average, SqlFunctionType.Sum};
                      
        public bool HasOutAggregates() {
          var result = this.Operands.OfType<SqlFunctionExpression>().Any(f => _aggregates.Contains(f.FunctionType));
          return result; 
        }
        public bool HasOrderBy() {
          return OrderBy.Count > 0; 
        }
        public bool HasLimit() {
          return Limit != null || OffsetAndLimit != null; 
        }

    }//class
}