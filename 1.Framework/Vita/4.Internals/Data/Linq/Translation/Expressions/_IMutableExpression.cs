
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// Allows an Expression to enumerator its Operands and be mutated, ie changing its operands.
    /// Depending on the Expression type (such as System.Linq.Expressions), a new copy may be returned.
    /// </summary>
    public interface IMutableExpression
    {
        /// <summary> Represents Expression operands, ie anything that is an expression. </summary>
        List<Expression> Operands { get; }

        /// <summary>Replaces operands and returns a corresponding expression.</summary>
        /// <param name="operands">New operands.</param>
        /// <returns>Mutated expression.</returns>
        Expression Mutate(IList<Expression> operands);
    }
}