using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Runtime;

using Vita.Data.Linq.Translation;
using Vita.Data.Linq.Translation.Expressions;
using System.Collections;
using Vita.Data.Driver;
using Vita.Entities.Model;
using Vita.Entities.Locking;

namespace Vita.Data.Linq {

  public static class LinqExpressionHelper {
    // There are 2 overloads of Select - we are interested in this one:
    //public static IQueryable<TResult> Select<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector);
    public static MethodInfo QueryableSelectMethod;
    public static MethodInfo QueryableWhereMethod;
    public static MethodInfo QueryableAsQueryableMethod;
    public static MethodInfo QueryableAny1ArgMethod;
    public static MethodInfo QueryableAny2ArgMethod;
    public static MethodInfo QueryableOrderByMethod;
    public static MethodInfo QueryableOrderByDescMethod;
    public static MethodInfo QueryableContainsMethod;


    public static MethodInfo ListContainsMethod;
    public static MethodInfo SessionEntitySetMethod;

    public static ConstructorInfo LinkTupleConstructorInfo;
    public static PropertyInfo LinkTupleLinkEntityPropertyInfo;
    public static PropertyInfo LinkTupleTargetEntityPropertyInfo;


    static MethodInfo _escapeLikePatternMethod;
    static MethodInfo _stringConcatMethod;
    static MethodInfo _convertToStringMethod;
    static MethodInfo _getUnderlyingValueOrDefaultMethod;
    private static object[] _emptyArray = new object[] { };


    #region Static constructor
    static LinqExpressionHelper() {
      var ti = typeof(LinqExpressionHelper).GetTypeInfo(); 
      _escapeLikePatternMethod = ti.GetDeclaredMethod(nameof(EscapeLikePattern));
      _stringConcatMethod = ti.GetDeclaredMethod(nameof(ConcatPattern));
      _getUnderlyingValueOrDefaultMethod = ti.GetDeclaredMethod(nameof(GetUnderlyingValueOrDefault));
      _convertToStringMethod = ti.GetDeclaredMethod(nameof(ConvertToString)); 
      // Find Queryable methods
      var allMethods = typeof(Queryable).GetTypeInfo().DeclaredMembers.OfType<MethodInfo>().ToList();
      foreach(var m in allMethods.Where(m => m.Name == nameof(Queryable.Select))) {
        var arg1 = m.GetParameters()[1];
        var paramType = arg1.ParameterType;
        var funcType = paramType.GetGenericArguments()[0];
        var funcGenDef = funcType.GetGenericTypeDefinition();
        if(funcGenDef == typeof(Func<,>))
          QueryableSelectMethod = m;
      }
      foreach(var m in allMethods.Where(m => m.Name == nameof(Queryable.Where))) {
        var arg1 = m.GetParameters()[1];
        var paramType = arg1.ParameterType;
        var funcType = paramType.GetGenericArguments()[0];
        var funcGenDef = funcType.GetGenericTypeDefinition();
        if(funcGenDef == typeof(Func<,>))
          QueryableWhereMethod = m;
      }
      QueryableOrderByMethod = allMethods.First(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2);
      QueryableOrderByDescMethod = allMethods.First(m => m.Name == nameof(Queryable.OrderByDescending) && m.GetParameters().Length == 2);
      QueryableAny1ArgMethod = allMethods.First(m => m.Name == nameof(Queryable.Any) && m.GetParameters().Length == 1);
      QueryableAny2ArgMethod = allMethods.First(m => m.Name == nameof(Queryable.Any) && m.GetParameters().Length == 2);
      QueryableAsQueryableMethod = allMethods.First(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethod);
      QueryableContainsMethod = typeof(Queryable).GetMethods().First(m => m.Name == "Contains" && m.GetParameters().Length == 2);

      LinkTupleLinkEntityPropertyInfo = typeof(LinkTuple).GetProperty(nameof(LinkTuple.LinkEntity));
      LinkTupleTargetEntityPropertyInfo = typeof(LinkTuple).GetProperty(nameof(LinkTuple.TargetEntity));
      LinkTupleConstructorInfo = typeof(LinkTuple).GetConstructor(Type.EmptyTypes);

      ListContainsMethod = typeof(ICollection<>).FindMethod(nameof(IList.Contains), paramCount: 1);  
      SessionEntitySetMethod = typeof(IEntitySession).FindMethod(nameof(IEntitySession.EntitySet));
    }
    #endregion 

    public static IList<Type> GetArgumentTypes(this Expression expression) {
      var mtExpr = expression as DerivedTableExpression;
      if(mtExpr != null)
        return GetArgumentTypes(mtExpr.SourceExpression);
      switch(expression.NodeType) {
        case ExpressionType.New:
          var newExpr = (NewExpression)expression; 
          var prms = newExpr.Constructor.GetParameters();
          return prms.Select(pi => pi.ParameterType).ToList(); 
        case ExpressionType.Call:
          //For method call the first operand is object defining the function; 
          var types = new List<Type>(); 
          var callExpr = (MethodCallExpression) expression;
          types.Add(callExpr.Method.DeclaringType);
          var mprms = callExpr.Method.GetParameters();
          types.AddRange(mprms.Select(p => p.ParameterType));
          return types; 
        default:
          return expression.GetOperands().Select(op => op.Type).ToList();
      }
    }


    public static SqlFunctionType ToBitwise(this ExpressionType type) {
      switch (type) {
        case ExpressionType.And: return SqlFunctionType.AndBitwise;
        case ExpressionType.Or: return SqlFunctionType.OrBitwise;
        case ExpressionType.ExclusiveOr: return SqlFunctionType.XorBitwise;
        default:
          Util.Throw("No bitwise form of expression type {0}.", type);
          return default(SqlFunctionType);
      }
    }

    public static bool IsBoolConst(this Expression expression, out bool value) {
      value = false; 
      if (expression.NodeType != ExpressionType.Constant || expression.Type != typeof(bool))
        return false; 
      var constExpr = (ConstantExpression) expression; 
      value = (bool) constExpr.Value; 
      return true; 
    }

    public static bool IsConstNull(this Expression expression) {
      if(expression.NodeType == ExpressionType.Constant && ((ConstantExpression)expression).Value == null)
        return true;
      if(expression.NodeType == ExpressionType.Convert) {
        var conv = (UnaryExpression)expression;
        if(IsConstNull(conv.Operand))
          return true;
      }
      return false;
    }

    public static bool IsConstZero(this Expression expression) {
      if (expression.NodeType != ExpressionType.Constant || !expression.Type.IsInt()) return false;
      var c = (ConstantExpression)expression;
      var iv = (int)c.Value;
      return iv == 0;
    }

    public static bool IsConst<T>(this Expression expression, out T value) {
      value = default(T);
      if (expression.NodeType != ExpressionType.Constant || expression.Type != typeof(T)) return false;
      var c = (ConstantExpression)expression;
      value = (T)c.Value;
      return true; 
    }

    public static Expression CheckNeedConvert(Expression expression, Type toType) {
      if (expression.Type == toType)
        return expression;
      if (toType == typeof(bool)) {
        //One special case - used for bit columns
        if (expression.Type.IsInt())
          return Expression.Equal(expression, Expression.Constant(1));
      }
      if (toType.IsValueType && expression.Type.IsNullableValueType()) {
        return CallGetUnderlyingValueOrDefault(expression);
      }
      // One special case, comes up in non-supported GroupBy statements
      if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        Util.Throw(@"Cannot translate LINQ query into SQL: unsupported expression; try rewriting the query. 
If you are using GroupBy facility, rewrite the query to return only aggregate values, not grouped lists of original records/entities. 
Details: failed converting sub-expression of type {0} to type {1}", expression.Type, toType);
      //don't know what to do, let's hope SQL swallows it 
      return Expression.Convert(expression, toType);
    }

    public static MethodInfo GetToStringConverter(Type type) {
      return _convertToStringMethod.MakeGenericMethod(type); 
    }

    public static string ConvertToString<T>(T value) {
      if (value == null)
        return string.Empty;
      return value.ToString(); 
    }


    private static Expression CallGetUnderlyingValueOrDefault(Expression argument) {
      var valueType = argument.Type.GetUnderlyingStorageClrType();
      var genMethod = _getUnderlyingValueOrDefaultMethod.MakeGenericMethod(valueType);
      var result = Expression.Call(null, genMethod, argument);
      return result; 
    }

    private static T GetUnderlyingValueOrDefault<T>(Nullable<T> value) where T : struct {
      return (value == null) ? default(T) : value.Value;
    }

    public static string EscapeLikePattern(string pattern, char escapeChar, char[] wildCards) {
      if(string.IsNullOrEmpty(pattern))
        return pattern;
      var toEscape = wildCards.Where(wc => pattern.Contains(wc)).ToList();
      if(toEscape.Count == 0)
        return pattern;
      //Do escape
      var escStr = escapeChar.ToString();
      var escaped = pattern;
      foreach(var wc in toEscape)
        escaped = escaped.Replace(wc.ToString(), escStr + wc);
      return escaped; 
    }

    // Encode expression representing a dynamic call to Escape method and concatination with before/after segments:
    //   return before + EscapeLikePattern(patternPrm) + after
    // This dynamic expr is used when argument of Like (in LINQ calls to string.Contains(s), string.StartsWith(s))
    //   is a local variable; in this case escaping must be embedded into a LINQ expression. 
    // For cases when pattern is string literal (ex: where ent.Name.Contains("x")), the escaping is done directly over the literal
    public static Expression CallEscapeLikePattern(Expression pattern, char escapeChar, char[] wildCards, string before, string after) {
      var exprEscChar = Expression.Constant(escapeChar);
      var exprWildCards = Expression.Constant(wildCards);
      var exprBefore = Expression.Constant(before, typeof(string));
      var exprAfter = Expression.Constant(after, typeof(string));
      var callEscape = Expression.Call(_escapeLikePatternMethod, pattern, exprEscChar, exprWildCards);
      var result = Expression.Call(_stringConcatMethod, exprBefore, callEscape, exprAfter);
      return result; 
    }

    // We could use string.Concat, but forming an expr is a trouble - it has a lot of overloads;
    // So for simplicity we use this trivial method
    public static string ConcatPattern(string before, string pattern, string after) {
      return before + pattern + after; 
    }

    public static bool IsNewConstant(NewExpression expression, out object value) {
      value = null;
      if(!expression.Type.IsDbPrimitive())
        return false; 
      if(expression.Arguments.Count == 0)
        return true;
      if(!expression.Arguments.All(a => a.NodeType == ExpressionType.Constant))
        return false;
      value = ExpressionHelper.Evaluate(expression);
      return true;
    }


    public static object EvaluateContextParameterExpression(EntitySession session, ParameterExpression parameter) {
      if (typeof(IEntitySession).IsAssignableFrom(parameter.Type))
        return session;
      var context = session.Context;
      if (parameter.Type == typeof(OperationContext))
        return context;
      var name = parameter.Name.ToLowerInvariant();
      switch (name) {
        case "userid":
          return context.User.UserId;
        case "username":
          return context.User.UserName;
        case "altuserid":
          return context.User.AltUserId;
        default:
          object value = null;
          Util.Check(context.TryGetValue(parameter.Name, out value),
                      "Expression parameter {0} not set in OperationContext.", parameter.Name);
          var prmType = parameter.Type;
          if (value.GetType() == prmType)
            return value;
          value = ConvertHelper.ChangeType(value, prmType);
          return value;
      }
    }

    public static bool CheckSubQueryInLocalVariable(MemberExpression node, out IQueryable subQuery) {
      if(node.Expression != null && node.Expression.NodeType == ExpressionType.Constant && node.Type.IsGenericQueryable()) {
        var objExpr = (ConstantExpression)node.Expression;
        subQuery = (IQueryable)node.Member.GetMemberValue(objExpr.Value);
        return true;
      }
      subQuery = null;
      return false;
    }

    public static bool IsEntitySetMethod(this MethodInfo method) {
      if(method.Name != nameof(IEntitySession.EntitySet) || !method.ReturnType.IsGenericType)
        return false;
      switch(method.DeclaringType.Name) {
        case nameof(IEntitySession):
        case nameof(EntitySession):
        case nameof(ViewHelper):
        case nameof(LockHelper):
          return true;
        default:
          return false;
      }
    }

    public static void EvaluateLocals(DynamicLinqCommand command) {
      var locals = command.Locals;
      if(locals.Count == 0) {
        command.ParamValues = _emptyArray;
        return;
      }
      // evaluate external parameters - they come from OperationContext
      var extValues = _emptyArray;
      if(command.ExternalParameters != null && command.ExternalParameters.Length > 0) {
        var ctx = command.Session.Context;
        extValues = new object[command.ExternalParameters.Length];
        for(int i = 0; i < extValues.Length; i++) {
          extValues[i] = LinqExpressionHelper.EvaluateContextParameterExpression(command.Session, command.ExternalParameters[i]);
        }//for i
      } //if 

      // evaluate locals
      command.ParamValues = new object[locals.Count];
      for(int i = 0; i < locals.Count; i++) {
        var localExpr = locals[i];
        var value = ExpressionHelper.Evaluate(localExpr, command.ExternalParameters, extValues);
        if (value != null && localExpr.Type.IsGenericType)
          value = ConvertParamValue(value, localExpr.Type);
        command.ParamValues[i] = value;  
      } 
    } //method

    // convert List<T> to array of T; npgsql fails with List<enum> but Ok with array of enum
    // It would be safer if we convert all enum values to ints, but so far providers are OK with passing enums directly
    private static object ConvertParamValue(object value, Type type) {
      if (value == null || !type.IsGenericType)
        return value;
      var genType = type.GetGenericTypeDefinition();
      var elemType = type.GetGenericArguments()[0];
      // we care only about List<enum>, npgsql fails for these
      if (genType != typeof(List<>) || !elemType.IsEnum)
        return value;
      // convert to array
      var list = value as IList;
      var arr = Array.CreateInstance(elemType, list.Count);
      for (int i = 0; i < arr.Length; i++)
        arr.SetValue(list[i], i);
      return arr; 
    }


  }//class
}
