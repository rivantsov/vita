using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Model;

namespace Vita.Entities.Utilities {
  public static class ExpressionHelper {

    public static Expression BuildChainedPropReader(Expression target, string propChain) {
      Expression expr = target;
      var propNames = propChain.SplitNames('.');
      foreach(var propName in propNames) {
        var propInfo = expr.Type.GetAllProperties().FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase)); //we have to ignore case
        Util.Check(propInfo != null, "Property {0} not found on type {1}. Property change: {2}", propName, expr.Type, propChain);
        expr = Expression.MakeMemberAccess(expr, propInfo);
      }
      var result = Expression.Convert(expr, typeof(object)); //needed for value types
      return result;
    }


    public static object Evaluate(Expression expression, ParameterExpression[] parameters = null, object[] paramValues = null) {
      if(expression == null)
        return null;
      try {
        //Check most common cases, to avoid lambda compile
        object result;
        switch(expression.NodeType) {
          case ExpressionType.Constant:
            var cexp = (ConstantExpression)expression;
            return cexp.Value;
          case ExpressionType.MemberAccess:
            var maExp = (MemberExpression)expression;
            if(maExp.Expression != null && maExp.Expression.NodeType != ExpressionType.Constant) 
              break;
            var objConst = (ConstantExpression)maExp.Expression;
            var obj = objConst == null ? null : objConst.Value;
            result = ReflectionHelper.GetMemberValue(maExp.Member, obj);
            return result; 
          case ExpressionType.Convert:
            var convExpr = (UnaryExpression)expression;
            var convOpnd = convExpr.Operand;
            if(convOpnd == null)
              break;
            if(convOpnd.Type.IsEnum && convExpr.Type == typeof(int)) {
              var opndValue = Evaluate(convOpnd, parameters, paramValues);
              return (int)opndValue;
            }
            break;
        }//switch NodeType

        // generic path: compile and evaluate
        if(parameters != null && parameters.Length > 0) {
          var fn = Expression.Lambda(expression, parameters).Compile();
          result = fn.DynamicInvoke(paramValues);
        } else {
          var fn = Expression.Lambda(expression).Compile();
          result = fn.DynamicInvoke();
        }
        return result;
      } catch(Exception ex) {
        var newMsg = Util.SafeFormat("{0} error when evaluating expression '{1}'", ex.Message, expression);
        throw new Exception(newMsg, ex); 
      }
    }

    public static LambdaExpression UnwrapLambda(Expression node) {
      // Sometimes lambda is wrapped in quote, sometimes in constant (?)
      switch(node.NodeType) {
        case ExpressionType.Lambda:
          return (LambdaExpression)node; 
        case ExpressionType.Quote:
          var op = ((UnaryExpression)node).Operand;
          return (LambdaExpression)op;
        case ExpressionType.Constant:
          var cn = (ConstantExpression)node;
          return (LambdaExpression)cn.Value;
        default:
          Util.Throw("Fatal error: expected lambda, recieved: {0}.", node.NodeType);
          return null; 
      }
    }

    public static void CheckIsQueryable(Expression expression) {
      var type = expression.Type;
      Util.Check(type.GetTypeInfo().IsGenericType, "Invalid query expression type ({0}) - must be generic type. ", type);
      var genericType = type.GetGenericTypeDefinition();
      Util.Check(genericType == typeof(IQueryable<>) || genericType == typeof(IOrderedQueryable<>),
                       "Invalid query expression type ({0}) - must be IQueryable<> or IOrderedQueryable<>. ", type);
    }

    public static string GetSelectedProperty<TArg>(Expression<Func<TArg, object>> memberSelector) {
      var errTemplate = "Invalid member selector expression: {0}. Must be a single property selector.";
      var target = memberSelector.Body;
      // we might have conversion expression here for value types - member access is under it
      if(target.NodeType == ExpressionType.Convert) {
        var unExpr = target as System.Linq.Expressions.UnaryExpression;
        target = unExpr.Operand;
      }
      var memberAccess = target as MemberExpression;
      Util.Check(memberAccess != null, errTemplate, memberSelector);
      return memberAccess.Member.Name;
    }

    // Perf-optimized nodeType.ToString()
    public static string AsString(this ExpressionType value) {
      if(_expressionTypeStrings == null)
        _expressionTypeStrings = BuildExpressionStrings();
      return _expressionTypeStrings[(int)value];
    }

    private static string[] _expressionTypeStrings;

    private static string[] BuildExpressionStrings() {
      var values = Enum.GetValues(typeof(ExpressionType));
      var len = 0;
      foreach(var v in values) {
        var i = (int)v;
        if(i > len)
          len = i; 
      }
      var strings = new string[len];
      foreach(var v in values) 
        strings[(int)v] = v.ToString();
      return strings; 
    } //method

  }
}
