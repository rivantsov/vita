using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {

  /// <summary>
  /// Extension methods used in building dynamic LINQ expressions, typically in search queries. 
  /// </summary>
  public static class FuncExtensions {

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expresssion,
                                                        Expression<Func<T, bool>> predicate) {
      var invokedExpr = Expression.Invoke(predicate, expresssion.Parameters.Cast<Expression>());
      return Expression.Lambda<Func<T, bool>>
            (Expression.OrElse(expresssion.Body, invokedExpr), expresssion.Parameters);
    }

    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> expression,
                                                         Expression<Func<T, bool>> predicate) {
      var invokedExpr = Expression.Invoke(predicate, expression.Parameters.Cast<Expression>());
      return Expression.Lambda<Func<T, bool>>
            (Expression.AndAlso(expression.Body, invokedExpr), expression.Parameters);
    }

    // extra utility methods - chaining methods with checks
    public static Expression<Func<T, bool>> OrIf<T>(this Expression<Func<T, bool>> expresssion,
                                                         bool condition, Expression<Func<T, bool>> predicate) {
      if(condition)
        return expresssion.Or(predicate);
      else
        return expresssion;
    }

    public static Expression<Func<T, bool>> OrIfNotNull<T>(this Expression<Func<T, bool>> expresssion,
                                                         object value, Expression<Func<T, bool>> predicate) {
      return expresssion.OrIf(!IsNullOrEmpty(value), predicate);
    }

    public static Expression<Func<T, bool>> AndIf<T>(this Expression<Func<T, bool>> expresssion,
                     bool condition, Expression<Func<T, bool>> predicate) {
      if(condition)
        return expresssion.And(predicate);
      else
        return expresssion;
    }

    public static Expression<Func<T, bool>> AndIfNotEmpty<T>(this Expression<Func<T, bool>> expresssion,
                       object value, Expression<Func<T, bool>> predicate) {
      return expresssion.AndIf(!IsNullOrEmpty(value), predicate);
    }


    private static bool IsNullOrEmpty(object value) {
      return value == null || value is string && string.IsNullOrWhiteSpace((string)value);
    }


  }//class
}
