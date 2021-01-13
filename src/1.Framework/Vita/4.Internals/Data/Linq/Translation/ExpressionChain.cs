using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Linq.Translation {

  public class ExpressionChain : List<Expression> {

    private ExpressionChain() { }

    public static ExpressionChain Build(Expression expression) {
      var exprChain = new ExpressionChain();
      Build(exprChain, expression);
      if (exprChain.Count > 1)
        exprChain.Reverse();
      if (exprChain.Count == 0)
        exprChain.Add(expression);
      return exprChain;
    }
    private static void Build(ExpressionChain chain, Expression expression) {
      if (expression.NodeType != ExpressionType.Call)
        return;
      var callExpr = expression as MethodCallExpression;
      if (callExpr.Object != null || callExpr.Method.DeclaringType != typeof(Queryable))
        return;
      chain.Add(callExpr);
      Build(chain, callExpr.Arguments[0]);
    }



  }
}
