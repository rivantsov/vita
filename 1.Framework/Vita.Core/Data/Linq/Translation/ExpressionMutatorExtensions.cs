
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.Linq.Translation {
    
    internal static class ExpressionMutatorExtensions
    {
        /// <summary>
        /// Enumerates all subexpressions related to this one
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static IList<Expression> GetOperands(this Expression expression)
        {
          var result = ExpressionMutator.GetOperands(expression);
          return result; 
        }

        public static T ChangeOperands<T>(this T expression, IList<Expression> operands, IEnumerable<Expression> oldOperands = null)
            where T : Expression
        {
            if (!HaveOperandsChanged(operands, oldOperands))
                return expression;
            var mutableExpression = expression as IMutableExpression;
            if (mutableExpression != null)
                return (T)mutableExpression.Mutate(operands);
          // RI: new version
            var result = (T) ExpressionMutator.Mutate(expression, operands);
            return result; 
            // return (T)ExpressionMutatorFactory.GetMutator(expression).Mutate(operands);
        }

        /// <summary>
        /// Determines if operands have changed for a given expression
        /// </summary>
        private static bool HaveOperandsChanged(IList<Expression> operands, IEnumerable<Expression> oldOperands = null)   {
          if (oldOperands == null)
            return true;
          var oldList = oldOperands.ToList(); 
          if (operands.Count != oldList.Count)
              return true;
          for (int operandIndex = 0; operandIndex < operands.Count; operandIndex++)
              if (operands[operandIndex] != oldList[operandIndex])
                  return true;
          return false;
        }

        /// <summary>
        /// Returns the expression result
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static object Evaluate(this Expression expression)
        {
            var executableExpression = expression as IExecutableExpression;
            if (executableExpression != null)
                return executableExpression.Execute();
            var lambda = Expression.Lambda(expression);
            var compiled = lambda.Compile();
            var value = compiled.DynamicInvoke();
            return value;
        }

        /// <summary>
        /// Down-top pattern analysis.
        /// </summary>
        /// <param name="expression">The original expression</param>
        /// <param name="analyzer"></param>
        /// <returns>A new QueryExpression or the original one</returns>
        public static Expression Recurse(this Expression expression, Func<Expression, Expression> analyzer)
        {
            var oldOperands = GetOperands(expression);
            var newOperands = new List<Expression>();
            // first, work on children (down)
            foreach (var operand in oldOperands)
            {
                if (operand != null)
                    newOperands.Add(Recurse(operand, analyzer));
                else
                    newOperands.Add(null);
            }
            // then on expression itself (top)
            return analyzer(expression.ChangeOperands(newOperands, oldOperands));
        }
    }
}