using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {
  public static class ExpressionHelper {

    public static Expression BuildChainedPropReader(Expression target, string propChain) {
      Expression expr = target;
      var propNames = propChain.SplitNames('.');
      foreach(var propName in propNames) {
        var propInfo = expr.Type.GetAllProperties().FirstOrDefault(p => p.Name.Equals(propName, StringComparison.InvariantCultureIgnoreCase)); //we have to ignore case
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
            var member = maExp.Member;
            if(maExp.Expression != null && maExp.Expression.NodeType != ExpressionType.Constant) 
              break;
            var objConst = (ConstantExpression)maExp.Expression;
            var obj = objConst == null ? null : objConst.Value;
            switch(maExp.Member.MemberType) {
              case MemberTypes.Field:
                var fld = (FieldInfo)maExp.Member;
                result = fld.GetValue(obj);
                return result;
              case MemberTypes.Property:
                var prop = (PropertyInfo)maExp.Member;
                result = prop.GetValue(obj, null);
                return result;
            }
            break;
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
        var newMsg = StringHelper.SafeFormat("{0} error when evaluating expression '{1}'", ex.Message, expression);
        throw new Exception(newMsg, ex); 
      }
    }

    public static Expression MakeSafeMemberAccess(MemberExpression node) {
      var objType = node.Expression.Type;
      if(!objType.IsInterface)
        return node;
      var ifTest = Expression.Equal(node.Expression, Expression.Constant(null, node.Expression.Type));
      var defaultValueExpr = Expression.Constant(ReflectionHelper.GetDefaultValue(node.Type), node.Type);
      var ifExpr = Expression.Condition(ifTest, defaultValueExpr, node,  node.Type);
      return ifExpr;
    }

    public static Expression Unqoute(Expression node) {
      if(node.NodeType != ExpressionType.Quote) return node;
      var unExpr = node as UnaryExpression;
      return unExpr.Operand;
    }


  }
}
