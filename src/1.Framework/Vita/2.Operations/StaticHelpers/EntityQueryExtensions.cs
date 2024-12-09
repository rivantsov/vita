using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Data;
using Vita.Data.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Entities {

  /// <summary>Defines options for an entity query. Use <c>WithOptions</c> extension method to set the options on a query. </summary>
  /// <remarks>Not all options are implemented.</remarks>
  [Flags]
  public enum QueryOptions {
    None = 0,
    NoEntityCache = 1,
    EntityCacheOnly = 1 << 1,
    NoQueryCache = 1 << 2,
    ForceIgnoreCase = 1 << 3,
    /// <summary>Do not use parameters in translated SQL, literals only. Used in views.</summary>
    NoParameters = 1 << 4,
  }



  public static class EntityQueryExtensions {

    public static bool IsSet(this QueryOptions options, QueryOptions option) {
      return (options & option) != 0;
    }

    public static IQueryable<T> OrderBy<T>(this IQueryable<T> query, string orderBySpec, IDictionary<string, string> nameMapping = null) {
      if(string.IsNullOrWhiteSpace(orderBySpec))
        return query;
      var memberInfos = typeof(T).GetAllProperties();
      string propRef;
      bool isDesc;
      var segments = orderBySpec.SplitNames();
      var resultQuery = query; 
      foreach(var segm in segments) {
        if(string.IsNullOrWhiteSpace(segm))
          continue;
        var arr = segm.SplitNames(':', '-'); // '-' is better for URLs, ':' is a special symbol, must be escaped
        if(arr.Length < 2) {
          propRef = segm;
          isDesc = false;
        } else {
          propRef = arr[0];
          var ascDesc = arr[1].Trim().ToUpper();
          if(!string.IsNullOrEmpty(ascDesc))
            Util.Check(ascDesc == "ASC" || ascDesc == "DESC", "Invalid OrderBy spec, ASC/DESC flag: '{0}'", ascDesc);
          isDesc = ascDesc == "DESC";
        }
        Util.Check(!string.IsNullOrWhiteSpace(propRef), "Invalid OrderBy spec, empty property ref: '{0}'", orderBySpec);
        //Build expression ent => ent.Prop
        string mappedName;
        if(nameMapping != null && nameMapping.TryGetValue(propRef, out mappedName))
          propRef = mappedName;
        //propRef might be a chain of reads: User.Person.FirstName; let's unpack it and build expression
        var prm = Expression.Parameter(typeof(T), "@ent");
        var memberGet = ExpressionHelper.BuildChainedPropReader(prm, propRef);
        var lambda = Expression.Lambda<Func<T, object>>(memberGet, prm);
        // Note: we might need to use ThenBy and ThenByDescending here for all clauses after first.
        // But it looks like it works OK, at least in SQL, the ORDER BY clause is correct
        resultQuery = isDesc ? resultQuery.OrderByDescending(lambda) : resultQuery.OrderBy(lambda);
      }
      return resultQuery;
    }//method

    internal static MethodInfo WithOptionsMethod;
    internal static MethodInfo IncludeMethod1;
    internal static MethodInfo IncludeMethod2;

    static EntityQueryExtensions() {
      WithOptionsMethod = typeof(EntityQueryExtensions).GetTypeInfo().GetDeclaredMethod("WithOptions");
      var includes = typeof(EntityQueryExtensions).GetTypeInfo().GetDeclaredMethods("Include");
      IncludeMethod1 = includes.First(m => m.GetGenericArguments().Length == 1);
      IncludeMethod2 = includes.First(m => m.GetGenericArguments().Length == 2);
    }

    /// <summary>Sets the options for a query. </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="query">Query</param>
    /// <param name="options">The options to set.</param>
    /// <returns>The original query with options set.</returns>
    /// <remarks>
    /// Use NoQueryCache option for search queries produced from custom search forms to prevent polluting the query cache with custom one-shot queries.
    /// Use NoLock option for adhoc queries against large transactional tables to avoid blocking access for concurrent update statements. 
    /// </remarks>
    public static IQueryable<TEntity> WithOptions<TEntity>(this IQueryable<TEntity> query, QueryOptions options) {
      //WithOptionsMethod = WithOptionsMethod ?? (MethodInfo) MethodInfo.GetCurrentMethod();
      var withGenMethod = WithOptionsMethod.MakeGenericMethod(typeof(TEntity));
      return query.Provider.CreateQuery<TEntity>(Expression.Call(withGenMethod, query.Expression, Expression.Constant(options)));
    }

    /// <summary>Adds an Include option (expression) to a query. The Include lambda body is an expression over the entity of the same type as query entity type.</summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="query">Base query.</param>
    /// <param name="include">An Include expression.</param>
    /// <returns>A new query expression with the include option.</returns>
    /// <remarks><para>The body of the Include lambda should return either an entity-type reference property of the entity passed as parameter, or its entity list property, 
    /// or an auto object with properties derived from described properties of the original entity. Example:</para>
    /// <example>
    ///   var query = session.EntitySet&lt;IBookReview&gt;().Where( . . .).Include(r =&gt; new {r.Book.Publisher, r.User});
    ///   var reviews = query.ToList(); 
    /// </example>
    /// </remarks>
    public static IQueryable<TEntity> Include<TEntity>(this IQueryable<TEntity> query, Expression<Func<TEntity, object>> include) {
      //IncludeMethod = IncludeMethod ?? (MethodInfo) MethodInfo.GetCurrentMethod();
      var genMethod = IncludeMethod1.MakeGenericMethod(typeof(TEntity));
      return query.Provider.CreateQuery<TEntity>(Expression.Call(genMethod, query.Expression, Expression.Quote(include)));
    }

    /// <summary>Adds an Include option (expression) to a query. The Include lambda </summary>
    /// <typeparam name="TEntity">Query entity type.</typeparam>
    /// <typeparam name="TTarget"></typeparam>
    /// <param name="query">Base query.</param>
    /// <param name="include">An Include expression.</param>
    /// <returns>A new query expression with the include option.</returns>
    public static IQueryable<TEntity> Include<TEntity, TTarget>(this IQueryable<TEntity> query, Expression<Func<TTarget, object>> include) {
      //IncludeMethod = IncludeMethod ?? (MethodInfo) MethodInfo.GetCurrentMethod();
      var genMethod = IncludeMethod2.MakeGenericMethod(typeof(TEntity), typeof(TTarget));
      return query.Provider.CreateQuery<TEntity>(Expression.Call(genMethod, query.Expression, Expression.Constant(include)));
    }

    public static IEntitySession GetSession<T>(this IQueryable<T> query) {
      var qProv = query.Provider as EntityQueryProvider;
      Util.Check(qProv != null, "Invalid argument 'query', expected entity query.");
      return qProv.Session; 
    }

  }//class
}
