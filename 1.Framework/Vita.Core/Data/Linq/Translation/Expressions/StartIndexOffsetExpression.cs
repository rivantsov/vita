using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace Vita.Data.Linq.Translation.Expressions
{
    public class StartIndexOffsetExpression : SqlExpression
    {
        public bool StartsAtOne{get; private set;}
        public Expression InnerExpression { get; private set; }

        public StartIndexOffsetExpression(bool startsAtOne, Expression startExpression) : base(SqlExpressionType.StartIndexOffset, typeof(int))
        {
            this.InnerExpression = startExpression;
            this.StartsAtOne = startsAtOne;
            Operands.Add(InnerExpression);
        }

        public override Expression Mutate(IList<Expression> newOperands)
        {
            this.InnerExpression = newOperands.First();
            return InnerExpression;
        }
    }
}
