
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.Linq.Translation {

    internal class SpecialExpressionTranslator 
    {
        /// <summary>
        /// Translate a hierarchy's SpecialExpressions to Expressions
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public Expression Translate(Expression expression)
        {
            return expression.Recurse(AnalyzeExpression);
        }

        protected virtual Expression AnalyzeExpression(Expression expression)
        {
            if (expression is SqlFunctionExpression)
                return Translate((SqlFunctionExpression)expression);
            else if (expression is StartIndexOffsetExpression)
                return Translate(((StartIndexOffsetExpression)expression).InnerExpression);
            return expression;
        }

        /// <summary>
        /// Translates a SpecialExpression to standard Expression equivalent
        /// </summary>
        /// <param name="sqlFunction"></param>
        /// <returns></returns>
        protected virtual Expression Translate(SqlFunctionExpression sqlFunction)
        {
            var operands = sqlFunction.Operands.ToList();
            switch (sqlFunction.FunctionType)  // SETuse
            {
                case SqlFunctionType.IsNull:
                    return TranslateIsNull(operands);
                case SqlFunctionType.IsNotNull:
                    return TranslateIsNotNull(operands);
                case SqlFunctionType.Concat:
                    return sqlFunction; 
                case SqlFunctionType.StringLength:
                    return TranslateStringLength(operands);
                case SqlFunctionType.ToUpper:
                    return GetStandardCallInvoke("ToUpper", operands);
                case SqlFunctionType.ToLower:
                    return GetStandardCallInvoke("ToLower", operands);
                case SqlFunctionType.StringInsert:
                    return GetStandardCallInvoke("Insert", operands);
                case SqlFunctionType.Substring:
                case SqlFunctionType.Trim:
                case SqlFunctionType.LTrim:
                case SqlFunctionType.RTrim:
                case SqlFunctionType.Replace:
                case SqlFunctionType.Remove:
                case SqlFunctionType.IndexOf:
                case SqlFunctionType.Year:
                case SqlFunctionType.Month:
                case SqlFunctionType.Day:
                case SqlFunctionType.Hour:
                case SqlFunctionType.Minute:
                case SqlFunctionType.Millisecond:
                case SqlFunctionType.Date:
                    return GetStandardCallInvoke(sqlFunction.FunctionType.ToString(), operands);
                case SqlFunctionType.Now:
                    return GetDateTimeNowCall(operands);
                case SqlFunctionType.DateDiffInMilliseconds:
                    return GetCallDateDiffInMilliseconds(operands);
                default:
                    Util.Throw("S0078: Implement translator for {0}", sqlFunction.FunctionType);
                    return null; 

            }
        }

        private Expression GetCallDateDiffInMilliseconds(List<Expression> operands)
        {
            return Expression.MakeMemberAccess(Expression.Subtract(operands.First(), operands.ElementAt(1)),
                                                typeof(TimeSpan).GetProperty("TotalMilliseconds"));
        }

        private Expression GetDateTimeNowCall(List<Expression> operands)
        {
            return Expression.Call(typeof(DateTime).GetProperty("Now").GetGetMethod());
        }

        private Expression TranslateStringLength(List<Expression> operands)
        {
            return Expression.MakeMemberAccess(operands[0], typeof(string).GetProperty("Length"));
        }

        protected virtual Expression GetStandardCallInvoke(string methodName, List<Expression> operands)
        {
            var parametersExpressions = operands.Skip(1);
            return Expression.Call(operands[0],
                                   operands[0].Type.GetMethod(methodName, parametersExpressions.Select(op => op.Type).ToArray()),
                                   parametersExpressions);
        }

        protected virtual Expression TranslateConcat(List<Expression> operands)
        {
            return Expression.Add(operands[0], operands[1]);
        }

        protected virtual Expression TranslateIsNotNull(List<Expression> operands)
        {
            return Expression.NotEqual(operands[0], Expression.Constant(null));
        }

        protected virtual Expression TranslateIsNull(List<Expression> operands)
        {
            return Expression.Equal(operands[0], Expression.Constant(null));
        }
    }
}