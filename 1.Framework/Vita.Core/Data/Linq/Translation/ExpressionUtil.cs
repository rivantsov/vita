using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Common;
using System.Collections;
using System.Data;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Data.Linq.Translation {

  public static class ExpressionUtil {
    // There are 2 overloads of Select - we are interested in this one:
    //public static IQueryable<TResult> Select<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector);
    public static MethodInfo QueryableSelectMethod;
    public static MethodInfo QueryableWhereMethod;
    public static MethodInfo QueryableAsQueryableMethod;
    public static MethodInfo QueryableCountMethod;
    public static MethodInfo SessionEntitySetMethod;

    static MethodInfo _escapeLikePatternMethod;
    static MethodInfo _stringConcatMethod;
    static MethodInfo _convertToStringMethod;
    static MethodInfo _getUnderlyingValueOrDefaultMethod; 

    static ExpressionUtil() {
      _escapeLikePatternMethod = typeof(ExpressionUtil).GetMethod("EscapeLikePattern", BindingFlags.Static | BindingFlags.Public);
      _stringConcatMethod = typeof(ExpressionUtil).GetMethod("ConcatPattern", BindingFlags.Static | BindingFlags.Public);
      _getUnderlyingValueOrDefaultMethod = typeof(ExpressionUtil).GetMethod("GetUnderlyingValueOrDefault", BindingFlags.Static | BindingFlags.NonPublic);
      _convertToStringMethod = typeof(ExpressionUtil).GetMethod("ConvertToString"); //, BindingFlags.Static | BindingFlags.Public);
      FindQueryableMethods(); 
    }
    private static void FindQueryableMethods() {
      var allMethods = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static);
      foreach(var m in allMethods.Where(m => m.Name == "Select")) {
        var arg1 = m.GetParameters()[1];
        var paramType = arg1.ParameterType;
        var funcType = paramType.GetGenericArguments()[0];
        var funcGenDef = funcType.GetGenericTypeDefinition();
        if(funcGenDef == typeof(Func<,>))
          QueryableSelectMethod = m;
      }
      foreach (var m in allMethods.Where(m => m.Name == "Where")) {
        var arg1 = m.GetParameters()[1];
        var paramType = arg1.ParameterType;
        var funcType = paramType.GetGenericArguments()[0];
        var funcGenDef = funcType.GetGenericTypeDefinition();
        if (funcGenDef == typeof(Func<,>))
          QueryableWhereMethod = m;
      }
      QueryableCountMethod = allMethods.First(m => m.Name == "Count" && m.GetParameters().Length == 1);
      QueryableAsQueryableMethod = allMethods.First(m => m.Name == "AsQueryable" && m.IsGenericMethod);
      SessionEntitySetMethod = typeof(IEntitySession).GetMethod("EntitySet");
    }

    public static IList<Type> GetArgumentTypes(this Expression expression) {
      var mtExpr = expression as MetaTableExpression;
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

    public static ConstantExpression MakeZero(Type type) {
      if(type == typeof(Int64))
        return Expression.Constant(0L);
      else
        return Expression.Constant(0);
    }

    public static BinaryExpression MakeGreaterThanZero(Expression expr) {
      var zeroExpr = ExpressionUtil.MakeZero(expr.Type);
      return Expression.GreaterThan(expr, zeroExpr);
    }


    public static BinaryExpression MakeBinary(ExpressionType nodeType, Expression left, Expression right) {
      // After replacing arguments with column expressions there might be a problem with nullable columns.
      // The following code compensates for this
      if (left.Type == right.Type || left.IsConstNull() || right.IsConstNull())
        return Expression.MakeBinary(nodeType, left, right);
      if (left.Type.CanAssignWithConvert(right.Type))
        return Expression.MakeBinary(nodeType, left, Expression.Convert(right, left.Type));
      if (right.Type.CanAssignWithConvert(left.Type))
        return Expression.MakeBinary(nodeType, Expression.Convert(left, right.Type), right);
      Util.Throw("Cannot create Binary expression for arguments of types {0} and {1}", left.Type, right.Type);
      return null; //never happens
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

    public static ConstantExpression CreateConstant(object value, Type type) {
      if(value == null || value.GetType() == type)
        return Expression.Constant(value, type);
      return Expression.Constant(Convert.ChangeType(value, type), type); 
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
      var valueType = argument.Type.GetUnderlyingType();
      var genMethod = _getUnderlyingValueOrDefaultMethod.MakeGenericMethod(valueType);
      var result = Expression.Call(null, genMethod, argument);
      return result; 
    }

    private static T GetUnderlyingValueOrDefault<T>(Nullable<T> value) where T : struct {
      return (value == null) ? default(T) : value.Value;
    }

    public static string EscapeLikePattern(string pattern, char escapeChar) {
      if(string.IsNullOrEmpty(pattern))
        return pattern;
      char[] wildCards = new char[] { '_', '%', '[', ']', escapeChar};
      var needsEscape = pattern.IndexOfAny(wildCards) >= 0;
      if(!needsEscape)
        return pattern;
      //Do escape
      var escStr = escapeChar.ToString();
      var escPattern = pattern
        .Replace(escStr, escStr + escStr)
        .Replace("_", escapeChar + "_")
        .Replace("%", escapeChar + "%")
        .Replace("[", escapeChar + "[")
        .Replace("]", escapeChar + "]");
      return escPattern;
    }

    // Encode expression representing a dynamic call to Escape method and concatination with before/after segments:
    //   return before + EscapeLikePattern(patternPrm) + after
    // This dynamic expr is used when argument of Like (in LINQ calls to string.Contains(s), string.StartsWith(s))
    //   is a local variable; in this case escaping must be embedded into a LINQ expression. 
    // For cases when pattern is string literal (ex: where ent.Name.Contains("x")), the escaping is done directly over the literal
    public static Expression CallEscapeLikePattern(Expression pattern, char escapeChar, string before, string after) {
      var exprEscChar = Expression.Constant(escapeChar);
      var exprBefore = Expression.Constant(before, typeof(string));
      var exprAfter = Expression.Constant(after, typeof(string));
      var callEscape = Expression.Call(_escapeLikePatternMethod, pattern, exprEscChar);
      var result = Expression.Call(_stringConcatMethod, exprBefore, callEscape, exprAfter);
      return result; 
    }

    // We could use string.Concat, but forming an expr is a trouble - it has a lot of overloads;
    // So for simplicity we use this trivial method
    public static string ConcatPattern(string before, string pattern, string after) {
      return before + pattern + after; 
    }

    /// <summary>
    /// Returns Expression precedence. Higher value means lower precedence.
    /// http://en.csharp-online.net/ECMA-334:_14.2.1_Operator_precedence_and_associativity
    /// We added the Clause precedence, which is the lowest
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    internal static ExpressionPrecedence GetPrecedence(Expression expression) {
      if(expression == null) // RI: Rare case, mostly erroneous query, to prevent NullRef exception
        return ExpressionPrecedence.Primary;
      if(expression is SqlFunctionExpression) {
        var functionType = ((SqlFunctionExpression)expression).FunctionType;
        switch(functionType) // SETuse
        {
          case SqlFunctionType.IsNull:
          case SqlFunctionType.IsNotNull:
          case SqlFunctionType.EqualNullables:
          case SqlFunctionType.StringEqual: 
            return ExpressionPrecedence.Equality;
          case SqlFunctionType.Concat:
            return ExpressionPrecedence.Additive;
          case SqlFunctionType.Like:
            return ExpressionPrecedence.Equality;
          // the following are methods
          case SqlFunctionType.Min:
          case SqlFunctionType.Max:
          case SqlFunctionType.Sum:
          case SqlFunctionType.Average:
          case SqlFunctionType.Count:
          case SqlFunctionType.Exists:
          case SqlFunctionType.StringLength:
          case SqlFunctionType.ToUpper:
          case SqlFunctionType.ToLower:
          case SqlFunctionType.Substring:
          case SqlFunctionType.Trim:
          case SqlFunctionType.LTrim:
          case SqlFunctionType.RTrim:
          case SqlFunctionType.StringInsert:
          case SqlFunctionType.Replace:
          case SqlFunctionType.Remove:
          case SqlFunctionType.IndexOf:
          case SqlFunctionType.Year:
          case SqlFunctionType.Month:
          case SqlFunctionType.Day:
          case SqlFunctionType.Hour:
          case SqlFunctionType.Minute:
          case SqlFunctionType.Second:
          case SqlFunctionType.Millisecond:
          case SqlFunctionType.Now:
          case SqlFunctionType.Date:
          case SqlFunctionType.Time:
          case SqlFunctionType.Week:
          case SqlFunctionType.DateDiffInMilliseconds:
          case SqlFunctionType.Abs:
          case SqlFunctionType.Exp:
          case SqlFunctionType.Floor:
          case SqlFunctionType.Ln:
          case SqlFunctionType.Log:
          case SqlFunctionType.Pow:
          case SqlFunctionType.Round:
          case SqlFunctionType.Sign:
          case SqlFunctionType.Sqrt:
          case SqlFunctionType.NewGuid:
            return ExpressionPrecedence.Primary;
          case SqlFunctionType.In:
          case SqlFunctionType.InArray:
            return ExpressionPrecedence.Equality; // not sure for this one
          case SqlFunctionType.AndBitwise:
            return ExpressionPrecedence.Multiplicative;
          case SqlFunctionType.OrBitwise:
          case SqlFunctionType.XorBitwise:
            return ExpressionPrecedence.Additive;
          case SqlFunctionType.ConvertBoolToBit:
            return ExpressionPrecedence.Clause;

          default:
            Util.Throw("Unhandled SqlFunction type: {0}", functionType);
            return default(ExpressionPrecedence); //never happens
        }
      }
      if(expression is SelectExpression)
        return ExpressionPrecedence.Clause;
      switch(expression.NodeType) {
        case ExpressionType.Add:
        case ExpressionType.AddChecked:
          return ExpressionPrecedence.Additive;
        case ExpressionType.And:
        case ExpressionType.AndAlso:
          return ExpressionPrecedence.ConditionalAnd;
        case ExpressionType.ArrayLength:
        case ExpressionType.ArrayIndex:
        case ExpressionType.Call:
        case ExpressionType.Parameter:
        case ExpressionType.New:
        case ExpressionType.NewArrayInit:
        case ExpressionType.NewArrayBounds:
        case ExpressionType.Invoke:
        case ExpressionType.Lambda:
        case ExpressionType.Quote:
          return ExpressionPrecedence.Primary;
        case ExpressionType.Coalesce:
          return ExpressionPrecedence.NullCoalescing;
        case ExpressionType.Conditional:
          return ExpressionPrecedence.Conditional;
        case ExpressionType.Constant:
          return ExpressionPrecedence.Primary;
        case ExpressionType.Convert:
        case ExpressionType.ConvertChecked:
          return ExpressionPrecedence.Primary;
        case ExpressionType.Divide:
          return ExpressionPrecedence.Multiplicative;
        case ExpressionType.Equal:
          return ExpressionPrecedence.Equality;
        case ExpressionType.ExclusiveOr:
          return ExpressionPrecedence.LogicalXor;
        case ExpressionType.GreaterThan:
        case ExpressionType.GreaterThanOrEqual:
          return ExpressionPrecedence.RelationalAndTypeTest;
        case ExpressionType.LeftShift:
          return ExpressionPrecedence.Shift;
        case ExpressionType.LessThan:
        case ExpressionType.LessThanOrEqual:
          return ExpressionPrecedence.RelationalAndTypeTest;
        case ExpressionType.ListInit:
        case ExpressionType.MemberAccess:
        case ExpressionType.MemberInit:
          return ExpressionPrecedence.Primary;
        case ExpressionType.Modulo:
        case ExpressionType.Multiply:
        case ExpressionType.MultiplyChecked:
          return ExpressionPrecedence.Multiplicative;
        case ExpressionType.Negate:
        case ExpressionType.UnaryPlus:
        case ExpressionType.NegateChecked:
          return ExpressionPrecedence.Unary;
        case ExpressionType.Not:
          return ExpressionPrecedence.Unary;
        case ExpressionType.NotEqual:
          return ExpressionPrecedence.Equality;
        case ExpressionType.Or:
        case ExpressionType.OrElse:
          return ExpressionPrecedence.ConditionalOr;
        case ExpressionType.RightShift:
          return ExpressionPrecedence.Shift;
        case ExpressionType.Subtract:
        case ExpressionType.SubtractChecked:
          return ExpressionPrecedence.Additive;
        case ExpressionType.TypeAs:
        case ExpressionType.TypeIs:
          return ExpressionPrecedence.RelationalAndTypeTest;
      }
      return ExpressionPrecedence.Primary;
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


  }//class
}
