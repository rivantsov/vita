using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Entities.Model {

  // Rewrites entity filter lambda body to protect against nulls. 
  // 'ent.Prop' is rewritten to allow ent to be null - in this case expr returns default for type
  internal class SafeMemberAccessRewriter : ExpressionVisitor {
    List<Expression> _skipNodes = new List<Expression>();

    public LambdaExpression Rewrite(LambdaExpression lambda) {
      return (LambdaExpression)Visit(lambda);
    }

    public override Expression Visit(Expression node) {
      if(_skipNodes.Contains(node))
        return node; 
      return base.Visit(node);
    }

    // Lambdas may include calls to functions that query database using custom Func<> expressions
    // Example: opContext.Exists<TEntity>(predicate) method. In this case Predicate should not be rewritten - 
    // it will be used for LINQ query when we execute the compiled lambda
    // We mark such parameters with DoNotRewrite attribute. 
    // We catch this attribute here and add corresponding arg value to _skipNodes list
    protected override Expression VisitMethodCall(MethodCallExpression node) {
      var prms = node.Method.GetParameters();
      for(int i = 0; i < prms.Length; i++) {
        var prmType = prms[i].ParameterType;
        if(prmType.IsGenericType && prmType.GetGenericTypeDefinition() == typeof(Expression<>)) {
          var hasAttr = prms[i].GetCustomAttributes(typeof(DoNotRewriteAttribute), true).Length > 0;
          if(hasAttr)
            _skipNodes.Add(node.Arguments[i]);
        }
      }
      return base.VisitMethodCall(node);
    }

    protected override Expression VisitMember(MemberExpression node) {
      var result = base.VisitMember(node);
      var memberExpr = result as MemberExpression;
      if(memberExpr != null)
        result = ExpressionHelper.MakeSafeMemberAccess(memberExpr);
      return result;
    }
  }//class


  /// <summary>Indicates that method parameter of Expression type should not be rewritten for safe member access. </summary>
  [AttributeUsage(AttributeTargets.Parameter)]
  public class DoNotRewriteAttribute : Attribute { }
}
