using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Data.Linq;

namespace Vita.Entities; 

public static class AsyncQueryExtensions {

  public static async Task<IList<TResult>> ToListAsync<TResult>(this IQueryable<TResult> query) {
    var asyncProv = query.Provider as IAsyncQueryProvider;
    Util.Check(asyncProv != null, "Invalid argument 'query', expected queryable with provider implementing IAsyncQueryProvider.");
    var objResult = await asyncProv.ExecuteAsync<IList<TResult>>(query.Expression);
    return (IList<TResult>)objResult;
  }

  /// <summary>Returns a list of entities.</summary>
  /// <typeparam name="TEntity">Entity type.</typeparam>
  /// <param name="session">Entity session.</param>
  /// <param name="skip">Optional. A number of entities to skip.</param>
  /// <param name="take">Maximum number of entities to include in results.</param>
  /// <param name="orderBy">Order by expression.</param>
  /// <param name="descending">Descening order flag.</param>
  /// <returns>A list of entities.</returns>
  public static async Task<IList<TEntity>> GetEntitiesAsync<TEntity>(this IEntitySession session,
        Expression<Func<TEntity, object>> orderBy = null, bool descending = false,
        int? skip = null, int? take = null) where TEntity : class {
    var query = session.EntitySet<TEntity>();
    if (orderBy != null)
      query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
    if (skip != null)
      query = query.Skip(skip.Value);
    if (take != null)
      query = query.Take(take.Value);
    return await query.ToListAsync();
  }

  public static async Task<TResult> FirstOrDefaultAsync<TResult>(this IQueryable<TResult> query) {
    var list1 = await query.Take(1).ToListAsync();
    if (list1.Count == 0)
      return default;
    else
      return list1[0];
  }

  public static async Task<TResult> FirstOrDefaultAsync<TResult>(this IQueryable<TResult> query, Expression<Func<TResult, bool>> pred) {
    var list1 = await query.Where(pred).Take(1).ToListAsync();
    if (list1.Count == 0)
      return default;
    else
      return list1[0];
  }

  public static async Task<TResult> FirstAsync<TResult>(this IQueryable<TResult> query) {
    var result = await FirstOrDefaultAsync<TResult>(query);
    if (result == null)
      throw new Exception("The collection is empty, cannot select the first element.");
    return result; 
  }
  public static async Task<TResult> FirstAsync<TResult>(this IQueryable<TResult> query, Expression<Func<TResult, bool>> pred) {
    var result = await FirstOrDefaultAsync<TResult>(query, pred);
    if (result == null)
      throw new Exception("The collection is empty, cannot select the first element.");
    return result;
  }



  public static Task<int> CountAsync<TSource>(this IQueryable<TSource> query) {
    Util.Check(query != null, "source");
    var asyncProv = query.Provider as IAsyncQueryProvider;
    Util.Check(asyncProv != null, "Invalid argument 'query', expected queryable with provider implementing IAsyncQueryProvider.");
    var methodInfo = _methodCount.MakeGenericMethod(typeof(TSource));
    var countCallExpr = Expression.Call(null, methodInfo, new[] { query.Expression });
    return asyncProv.ExecuteAsync<int>(countCallExpr);
  }

  private static readonly MethodInfo _methodCount;
  private static readonly MethodInfo _methodCountPred;

  static AsyncQueryExtensions() {
    var countMethods = typeof(Queryable).GetMember("Count", BindingFlags.Static | BindingFlags.Public).OfType<MethodInfo>();
    _methodCount = countMethods.First(m => m.GetParameters().Length == 1);
    _methodCountPred = countMethods.First(m => m.GetParameters().Length == 2);
  }


}
