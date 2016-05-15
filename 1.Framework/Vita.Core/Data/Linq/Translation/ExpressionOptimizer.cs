
using System;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.Linq.Translation {

    /// <summary>
    /// Optimizes expressions (such as constant chains)
    /// </summary>
    internal class ExpressionOptimizer  {
        public virtual Expression Optimize(Expression expression, TranslationContext rewriterContext)
        {
            return expression.Recurse(e => Analyze(e, rewriterContext));
        }

        protected Expression Analyze(Expression expression, TranslationContext rewriterContext)
        {
            // small optimization
            if (expression is ConstantExpression)
                return expression;

            // RI: handling comparison with NULL is moved to ExpressionDispatcher
            //expression = AnalyzeNull(expression, rewriterContext);
            expression = AnalyzeNot(expression, rewriterContext);
            expression = AnalyzeBinaryBoolean(expression, rewriterContext);
            // constant optimization at last, because the previous optimizations may generate constant expressions
            //RI: disabled this
            //expression = AnalyzeConstant(expression, rewriterContext);
            return expression;
        }

        private Expression AnalyzeBinaryBoolean(Expression expression, TranslationContext rewriterContext)
        {
            if (expression.Type != typeof(bool))
                return expression;
            var bin = expression as BinaryExpression;
            if (bin == null)
                return expression;
            bool canOptimizeLeft = bin.Left.NodeType == ExpressionType.Constant && bin.Left.Type == typeof(bool);
            bool canOptimizeRight = bin.Right.NodeType == ExpressionType.Constant && bin.Right.Type == typeof(bool);
            if (canOptimizeLeft && canOptimizeRight)
                return Expression.Constant(expression.Evaluate());
            if (canOptimizeLeft || canOptimizeRight)
                switch (expression.NodeType)
                {
                    case ExpressionType.AndAlso:
                        if (canOptimizeLeft)
                            if ((bool)bin.Left.Evaluate())
                                return bin.Right;   // (TRUE and X) == X 
                            else
                                return bin.Left;    // (FALSE and X) == FALSE 
                        if (canOptimizeRight)
                            if ((bool)bin.Right.Evaluate())
                                return bin.Left;    // (X and TRUE) == X 
                            else
                                return bin.Right;   // (X and FALSE) == FALSE
                        break;
                    case ExpressionType.OrElse:
                        if (canOptimizeLeft)
                            if ((bool)bin.Left.Evaluate())
                                return bin.Left;    // (TRUE or X) == TRUE 
                            else
                                return bin.Right;   // (FALSE or X) == X 
                        if (canOptimizeRight)
                            if ((bool)bin.Right.Evaluate())
                                return bin.Right;   // (X or TRUE) == TRUE 
                            else
                                return bin.Left;    // (X or FALSE) == X
                        break;
                    case ExpressionType.Equal:
                        // TODO: this optimization should work for Unary Expression Too
                        // this actually produce errors becouse of string based Sql generation
                        canOptimizeLeft = canOptimizeLeft && bin.Right is BinaryExpression;
                        if (canOptimizeLeft)
                            if ((bool)bin.Left.Evaluate())
                                return bin.Right;                   // (TRUE == X) == X 
                            else
                                return Expression.Not(bin.Right);   // (FALSE == X) == not X 
                        canOptimizeRight = canOptimizeRight && bin.Left is BinaryExpression;
                        // TODO: this optimization should work for Unary Expression Too
                        // this actually produce errors becouse of string based Sql generation
                        if (canOptimizeRight)
                            if ((bool)bin.Right.Evaluate())
                                return bin.Left;                    // (X == TRUE) == X 
                            else
                                return Expression.Not(bin.Left);    // (X == FALSE) == not X
                        break;
                    case ExpressionType.NotEqual:
                        canOptimizeLeft = canOptimizeLeft && bin.Right is BinaryExpression;
                        // TODO: this optimization should work for Unary Expression Too
                        // this actually produce errors becouse of string based Sql generation
                        if (canOptimizeLeft)
                            if ((bool)bin.Left.Evaluate())
                                return Expression.Not(bin.Right);   // (TRUE != X) == not X 
                            else
                                return bin.Right;                   // (FALSE != X) == X 
                        canOptimizeRight = canOptimizeRight && bin.Left is BinaryExpression;
                        // TODO: this optimization should work for Unary Expression Too
                        // this actually produce errors becouse of string based Sql generation
                        if (canOptimizeRight)
                            if ((bool)bin.Right.Evaluate())
                                return Expression.Not(bin.Left);    // (X != TRUE) == not X 
                            else
                                return bin.Left;                    // (X != FALSE) == X
                        break;
                }
            return expression;
        }


        protected virtual Expression AnalyzeNot(Expression expression, TranslationContext rewriterContext)
        {
            if (expression.NodeType == ExpressionType.Not)
            {
                var notExpression = expression as UnaryExpression;
                var subExpression = notExpression.Operand;
                var subOperands = subExpression.GetOperands().ToArray();
                switch (subExpression.NodeType)
                {
                    case ExpressionType.Equal:
                        return Expression.NotEqual(subOperands[0], subOperands[1]);
                    case ExpressionType.GreaterThan:
                        return Expression.LessThanOrEqual(subOperands[0], subOperands[1]);
                    case ExpressionType.GreaterThanOrEqual:
                        return Expression.LessThan(subOperands[0], subOperands[1]);
                    case ExpressionType.LessThan:
                        return Expression.GreaterThanOrEqual(subOperands[0], subOperands[1]);
                    case ExpressionType.LessThanOrEqual:
                        return Expression.GreaterThan(subOperands[0], subOperands[1]);
                    case ExpressionType.Not:
                        return subOperands[0]; // not not x -> x :)
                    case ExpressionType.NotEqual:
                        return Expression.Equal(subOperands[0], subOperands[1]);
                    case ExpressionType.Extension: 
                      if (subExpression is SqlFunctionExpression) {
                        var funcExpr = (SqlFunctionExpression) subExpression;
                        switch(funcExpr.FunctionType) {
                          case SqlFunctionType.IsNotNull: 
                            return new SqlFunctionExpression(SqlFunctionType.IsNull, typeof(bool), subOperands);
                          case SqlFunctionType.IsNull: 
                            return new SqlFunctionExpression(SqlFunctionType.IsNotNull, typeof(bool), subOperands);
                        }
                      }//if
                      break; 
                }
            }
            return expression;
        }

    }
}