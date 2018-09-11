
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// Represents a ORDER column to be sorted on
    /// </summary>
    public class OrderByExpression : SqlExpression
    {
        public bool Descending { get; set; }
        public Expression ColumnExpression { get; set; }

        //Special constructor for fake OrderBy:  'ORDER BY (SELECT 1)' used in MsSql, Postgres queries with Take/Skip - which require OrderBy clause
        public OrderByExpression() : base(SqlExpressionType.OrderBy, typeof(int)) { }

        public OrderByExpression(bool descending, Expression columnExpression) : base(SqlExpressionType.OrderBy, columnExpression.Type)
        {
            Descending = descending;
            ColumnExpression = columnExpression;
            Operands.Add(ColumnExpression);
        }

        public override Expression Mutate(System.Collections.Generic.IList<Expression> newOperands)
        {
            if (newOperands != null && newOperands.Count > 0)
              ColumnExpression = newOperands[0];
            return this;
        }

    }
}