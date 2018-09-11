
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Vita.Data.Linq.Translation.Expressions
{
    public abstract class OperandsMutableSqlExpression : SqlExpression
    {
        protected OperandsMutableSqlExpression(SqlExpressionType sqlExpressionType, Type type, IList<Expression> operands)
            : base(sqlExpressionType, type)
        {
            if(operands !=null)
              this.Operands.AddRange(operands);
        }

        /// <summary>
        /// Must be implemented by inheritors. I had no better name. Suggestions welcome
        /// </summary>
        /// <param name="operands"></param>
        /// <returns></returns>
        protected abstract Expression Mutate2(IList<Expression> operands);

        public override Expression Mutate(IList<Expression> newOperands)
        {
            return Mutate2(newOperands);
        }
    }
}