using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Linq.Expressions;

using Vita.Common;

namespace Vita.Entities.Caching {

  // A static class containing a map of pairs of Queryable/Enumerable methods.
  // This map is used by CacheQueryRewriter when rewriting a database query into a query against cached entity lists. 
  // During rewrite Queryable method calls are replaced with matching Enumerable methods.
  public static class QueryableToEnumerableMapping {

    public static MethodInfo GetGenericEnumerableFor(MethodInfo queryableMethod) {
      if (queryableMethod.DeclaringType != typeof(Queryable))
        return null;
      var enumMethod = QueryableToEnumerableMapping.GetEnumerableFor(queryableMethod);
      if (enumMethod == null)
        return null;
      if (enumMethod.IsGenericMethodDefinition) {
        var methodGenArgs = queryableMethod.GetGenericArguments();
        return enumMethod.MakeGenericMethod(methodGenArgs);
      } else
        return enumMethod;
    }

    public static MethodInfo GetEnumerableFor(MethodInfo queryableMethod) {
      CheckInitialized();
      MethodInfo qMethod =  queryableMethod;
      if (queryableMethod.IsGenericMethod)
        qMethod = queryableMethod.GetGenericMethodDefinition();
      MethodInfo enumerableMethod; 
      if (_map.TryGetValue(qMethod, out enumerableMethod)) 
        return enumerableMethod;
      return null; 
    }

    #region private fields
    static Dictionary<MethodInfo, MethodInfo> _map;
    static HashSet<MethodInfo> _enumerableMethods;
    private static bool _initialized;
    private static object _lockObject = new object(); 

    public static void CheckInitialized() {
      if (_initialized) 
        return; 
      lock(_lockObject)
        if (!_initialized) {
          Init();
          _initialized = true; 
        }
    }
    #endregion

    #region Map initialization
    public static void Init() {
      _map = new Dictionary<MethodInfo, MethodInfo>();
      _enumerableMethods = new HashSet<MethodInfo>();

      //Some dummy objects of specific types. They help us to denote a specific overload of Queryable and Enumerable methods, 
      // when we call 'Add'. The 'Add' method accepts two expressions as parameters, and it extracts the matching Queryable/Enumerable
      // methods from these expressions, and add the pair to the Map dictionary.
      // We use dummy classes TSource, TKey, etc - defined below.
      IQueryable<TSource> qS = null;
      IOrderedQueryable<TSource> qSord = null;
      IEnumerable<TSource> eS = null;
      IOrderedEnumerable<TSource> eSord = null;
      TSource objS = null;
      TKey objK = null; 
      TKeyComparer objKComp = null; 
      TSourceComparer objSComp = null;
      TElement objEl = null; 

      //Aggregate
      Add(() => Queryable.Aggregate(qS, (s, ss) => objS), ()=> Enumerable.Aggregate(eS, (s, ss) => objS));
      Add(() => Queryable.Aggregate(qS, objEl, (el, s) => objEl), () => Enumerable.Aggregate(eS, objEl, (el, s) => objEl));
      Add(() => Queryable.Aggregate(qS, objEl, (el, s) => objEl, x=>0), () => Enumerable.Aggregate(eS, objEl, (el, s) => objEl, x=>0));
      //All, Any
      Add(() => Queryable.All(qS, s => true), () => Enumerable.All(eS, s=> true));
      Add(() => Queryable.Any(qS), () => Enumerable.Any(eS));
      Add(() => Queryable.Any(qS, s => true), () => Enumerable.Any(eS, s => true));

      // Average
      Add(() => Queryable.Average(QueryOf<decimal>()), () => Enumerable.Average(QueryOf<decimal>()));
      Add(() => Queryable.Average(QueryOf<decimal?>()), () => Enumerable.Average(QueryOf<decimal?>()));
      Add(() => Queryable.Average(QueryOf<double>()), () => Enumerable.Average(QueryOf<double>()));
      Add(() => Queryable.Average(QueryOf<double?>()), () => Enumerable.Average(QueryOf<double?>()));
      Add(() => Queryable.Average(QueryOf<float>()), () => Enumerable.Average(QueryOf<float>()));
      Add(() => Queryable.Average(QueryOf<float?>()), () => Enumerable.Average(QueryOf<float?>()));
      Add(() => Queryable.Average(QueryOf<int>()), () => Enumerable.Average(QueryOf<int>()));
      Add(() => Queryable.Average(QueryOf<int?>()), () => Enumerable.Average(QueryOf<int?>()));
      Add(() => Queryable.Average(QueryOf<long>()), () => Enumerable.Average(QueryOf<long>()));
      Add(() => Queryable.Average(QueryOf<long?>()), () => Enumerable.Average(QueryOf<long?>()));
      Add(() => Queryable.Average(qS, x => (decimal)0), () => Enumerable.Average(eS, x => (decimal)0));
      Add(() => Queryable.Average(qS, x => (decimal?)0), () => Enumerable.Average(eS, x => (decimal?)0));
      Add(() => Queryable.Average(qS, x => (double)0), () => Enumerable.Average(eS, x => (double)0));
      Add(() => Queryable.Average(qS, x => (double?)0), () => Enumerable.Average(eS, x => (double?)0));
      Add(() => Queryable.Average(qS, x => (float)0), () => Enumerable.Average(eS, x => (float)0));
      Add(() => Queryable.Average(qS, x => (float?)0), () => Enumerable.Average(eS, x => (float?)0));
      Add(() => Queryable.Average(qS, x => (int)0), () => Enumerable.Average(eS, x => (int)0));
      Add(() => Queryable.Average(qS, x => (int?)0), () => Enumerable.Average(eS, x => (int?)0));
      Add(() => Queryable.Average(qS, x => (long)0), () => Enumerable.Average(eS, x => (long)0));
      Add(() => Queryable.Average(qS, x => (long?)0), () => Enumerable.Average(eS, x => (long?)0));
      
      //Cast
      Add(() => Queryable.Cast<TElement>(qS), () => Enumerable.Cast<TElement>(eS));
      // Concat
      Add(() => Queryable.Concat(qS, eS), () => Enumerable.Concat(eS, eS));
      //Contains
      Add(() => Queryable.Contains(qS, objS), () => Enumerable.Contains(eS, objS));
      Add(() => Queryable.Contains(qS, objS, objSComp), () => Enumerable.Contains(eS, objS, objSComp));
      //Count
      Add(() => Queryable.Count(qS), () => Enumerable.Count(eS));
      Add(() => Queryable.Count(qS, s => true), () => Enumerable.Count(eS, s => true));
      //DefaultIfEmpty
      Add(() => Queryable.DefaultIfEmpty(qS), () => Enumerable.DefaultIfEmpty(eS));
      Add(() => Queryable.DefaultIfEmpty(qS, objS), () => Enumerable.DefaultIfEmpty(eS, objS));
      //Distinct
      Add(() => Queryable.Distinct(qS), () => Enumerable.Distinct(eS));
      Add(() => Queryable.Distinct(qS, objSComp), () => Enumerable.Distinct(eS, objSComp));
      // ElementAt
      Add(() => Queryable.ElementAt(qS, 0), () => Enumerable.ElementAt(eS, 0));
      Add(() => Queryable.ElementAtOrDefault(qS, 0), () => Enumerable.ElementAtOrDefault(eS, 0));
      // Except 
      Add(() => Queryable.Except(qS, qS), () => Enumerable.Except(eS, eS));
      Add(() => Queryable.Except(qS, qS, objSComp), () => Enumerable.Except(eS, eS, objSComp));
      // First
      Add(() => Queryable.First(qS), () => Enumerable.First(eS));
      Add(() => Queryable.First(qS, s=>true), () => Enumerable.First(eS, s=>true));
      Add(() => Queryable.FirstOrDefault(qS), () => Enumerable.FirstOrDefault(eS));
      Add(() => Queryable.FirstOrDefault(qS, s => true), () => Enumerable.FirstOrDefault(eS, s => true));
      // GroupBy
      Add(() => Queryable.GroupBy(qS, s=>objK), () => Enumerable.GroupBy(eS, s=>objK));
      Add(() => Queryable.GroupBy(qS, s => objK, (s, ss) => 0), () => Enumerable.GroupBy(eS, s => objK, (s, ss) => 0));
      Add(() => Queryable.GroupBy(qS, s => objK, (s) => 0), () => Enumerable.GroupBy(eS, s => objK, (s) => 0));
      Add(() => Queryable.GroupBy(qS, s => objS, objSComp), () => Enumerable.GroupBy(eS, s => s, objSComp));
      Add(() => Queryable.GroupBy(qS, s => objS, (s, ss) => 0, objSComp), () => Enumerable.GroupBy(eS, s => objS, (s, ss) => 0, objSComp));
      Add(() => Queryable.GroupBy(qS, s => objS, s => objK, (s, ss) => 0), () => Enumerable.GroupBy(eS, s => objS, s => objK, (s, ss) => 0));
      Add(() => Queryable.GroupBy(qS, s => objK, s => objEl, objKComp), () => Enumerable.GroupBy(eS, s => objK, s => objEl, objKComp));
      Add(() => Queryable.GroupBy(qS, s => objS, s => objK, (s, ss) => 0, objSComp), () => Enumerable.GroupBy(eS, s => objS, s => objK, (s, ss) => 0, objSComp));
      //GroupJoin
      IEnumerable<TSource2> eInner = null;
      Add(() => Queryable.GroupJoin(qS, eInner, s => objK, s => objK, (s, ss) => objEl), 
          () => Enumerable.GroupJoin(eS, eInner, s => objK, s => objK, (s, ss) => objEl));
      Add(() => Queryable.GroupJoin(qS, eInner, s => objK, s => objK, (s, ss) => objEl, objKComp), 
          () => Enumerable.GroupJoin(eS, eInner, s => objK, s => objK, (s, ss) => objEl, objKComp));
      //Intersect
      Add(() => Queryable.Intersect(qS, eS), () => Enumerable.Intersect(eS, eS));
      Add(() => Queryable.Intersect(qS, eS, objSComp), () => Enumerable.Intersect(eS, eS, objSComp));

      // Join 
      Add(() => Queryable.Join(qS, eInner, s => objK, s => objK, (s, ss) => objEl), () => Enumerable.Join(eS, eInner, s => objK, s => objK, (s, ss) => objEl));
      Add(() => Queryable.Join(qS, eInner, s => objK, s => objK, (s, ss) => objEl, objKComp), () => Enumerable.Join(eS, eInner, s => objK, s => objK, (s, ss) => objEl, objKComp));
      // Last
      Add(() => Queryable.Last(qS), () => Enumerable.Last(eS));
      Add(() => Queryable.Last(qS, s => true), () => Enumerable.Last(eS, s => true));
      Add(() => Queryable.LastOrDefault(qS), () => Enumerable.LastOrDefault(eS));
      Add(() => Queryable.LastOrDefault(qS, s => true), () => Enumerable.LastOrDefault(eS, s => true));
      //LongCount
      Add(() => Queryable.LongCount(qS), () => Enumerable.LongCount(eS));
      Add(() => Queryable.LongCount(qS, s => true), () => Enumerable.LongCount(eS, s => true));
      // Max, Min
      Add(() => Queryable.Max(qS), () => Enumerable.Max(eS));
      Add(() => Queryable.Max(qS, s => objEl), () => Enumerable.Max(eS, s => objEl)); //Enumerable has many Max versions - make sure we pick generic one here
      Add(() => Queryable.Min(qS), () => Enumerable.Min(eS));
      Add(() => Queryable.Min(qS, s => objEl), () => Enumerable.Min(eS, s => objEl)); //Enumerable has many Min versions - make sure we pick generic one here
      // OfType
      Add(() => Queryable.OfType<TElement>(qS), () => Enumerable.OfType<TElement>(eS)); //have to specify type arg explicitly
      // OrderBy
      Add(() => Queryable.OrderBy(qS, (s) => objK), () => Enumerable.OrderBy(eS, (s) => objK));
      Add(() => Queryable.OrderBy(qS, (s) => objK, objKComp), () => Enumerable.OrderBy(eS, (s) => objK, objKComp));
      Add(() => Queryable.OrderByDescending(qS, (s) => true), () => Enumerable.OrderByDescending(eS, (s) => true));
      Add(() => Queryable.OrderByDescending(qS, (s) => objK, objKComp), () => Enumerable.OrderByDescending(eS, (s) => objK, objKComp));
      // Reverse
      Add(() => Queryable.Reverse(qS), () => Enumerable.Reverse(eS));
      //Select
      Add(() => Queryable.Select(qS, (s) => true), () => Enumerable.Select(eS, (s) => true));
      Add(() => Queryable.Select(qS, (s, i) => true), () => Enumerable.Select(eS, (s, i) => true));
      //SelectMany
      IEnumerable<TElement> objListOfEl = null;
      Add(() => Queryable.SelectMany(qS, (s) => objListOfEl), () => Enumerable.SelectMany(eS, (s) => objListOfEl));
      Add(() => Queryable.SelectMany(qS, (s, i) => objListOfEl), () => Enumerable.SelectMany(eS, (s, i) => objListOfEl));
      Add(() => Queryable.SelectMany(qS, s => objListOfEl, (s, ss) => objEl), () => Enumerable.SelectMany(eS, s => objListOfEl, (s, ss) => objEl));
      Add(() => Queryable.SelectMany(qS, (s, i) => objListOfEl, (s, ss) => objEl), () => Enumerable.SelectMany(eS, (s, i) => objListOfEl, (s, ss) => objEl));
      // SequenceEqual
      Add(() => Queryable.SequenceEqual(qS, qS), () => Enumerable.SequenceEqual(eS, eS));
      Add(() => Queryable.SequenceEqual(qS, qS, objSComp), () => Enumerable.SequenceEqual(eS, eS, objSComp));
      // Single
      Add(() => Queryable.Single(qS), () => Enumerable.Single(eS));
      Add(() => Queryable.Single(qS, s => true), () => Enumerable.Single(eS, s => true));
      Add(() => Queryable.SingleOrDefault(qS), () => Enumerable.SingleOrDefault(eS));
      Add(() => Queryable.SingleOrDefault(qS, s => true), () => Enumerable.SingleOrDefault(eS, s => true));
      // Skip, SkipWhile
      Add(() => Queryable.Skip(qS, 1), () => Enumerable.Skip(eS, 1));
      Add(() => Queryable.SkipWhile(qS, s => true), () => Enumerable.SkipWhile(eS, s => true));
      Add(() => Queryable.SkipWhile(qS, (s, i) => true), () => Enumerable.SkipWhile(eS, (s, i) => true));
      // Sum
      Add(() => Queryable.Sum(QueryOf<decimal>()), () => Enumerable.Sum(QueryOf<decimal>()));
      Add(() => Queryable.Sum(QueryOf<decimal?>()), () => Enumerable.Sum(QueryOf<decimal?>()));
      Add(() => Queryable.Sum(QueryOf<double>()), () => Enumerable.Sum(QueryOf<double>()));
      Add(() => Queryable.Sum(QueryOf<double?>()), () => Enumerable.Sum(QueryOf<double?>()));
      Add(() => Queryable.Sum(QueryOf<float>()), () => Enumerable.Sum(QueryOf<float>()));
      Add(() => Queryable.Sum(QueryOf<float?>()), () => Enumerable.Sum(QueryOf<float?>()));
      Add(() => Queryable.Sum(QueryOf<int>()), () => Enumerable.Sum(QueryOf<int>()));
      Add(() => Queryable.Sum(QueryOf<int?>()), () => Enumerable.Sum(QueryOf<int?>()));
      Add(() => Queryable.Sum(QueryOf<long>()), () => Enumerable.Sum(QueryOf<long>()));
      Add(() => Queryable.Sum(QueryOf<long?>()), () => Enumerable.Sum(QueryOf<long?>()));
      Add(() => Queryable.Sum(qS, x => (decimal)0), () => Enumerable.Sum(eS, x => (decimal)0));
      Add(() => Queryable.Sum(qS, x => (decimal?)0), () => Enumerable.Sum(eS, x => (decimal?)0));
      Add(() => Queryable.Sum(qS, x => (double)0), () => Enumerable.Sum(eS, x => (double)0));
      Add(() => Queryable.Sum(qS, x => (double?)0), () => Enumerable.Sum(eS, x => (double?)0));
      Add(() => Queryable.Sum(qS, x => (float)0), () => Enumerable.Sum(eS, x => (float)0));
      Add(() => Queryable.Sum(qS, x => (float?)0), () => Enumerable.Sum(eS, x => (float?)0));
      Add(() => Queryable.Sum(qS, x => (int)0), () => Enumerable.Sum(eS, x => (int)0));
      Add(() => Queryable.Sum(qS, x => (int?)0), () => Enumerable.Sum(eS, x => (int?)0));
      Add(() => Queryable.Sum(qS, x => (long)0), () => Enumerable.Sum(eS, x => (long)0));
      Add(() => Queryable.Sum(qS, x => (long?)0), () => Enumerable.Sum(eS, x => (long?)0));
      
      // Take, TakeWhile
      Add(() => Queryable.Take(qS, 1), () => Enumerable.Take(eS, 1));
      Add(() => Queryable.TakeWhile(qS, s => true), () => Enumerable.TakeWhile(eS, s => true));
      Add(() => Queryable.TakeWhile(qS, (s, i) => true), () => Enumerable.TakeWhile(eS, (s, i) => true));

      // ThenBy, ThenByDescending
      Add(() => Queryable.ThenBy(qSord, (s) => objK), () => Enumerable.ThenBy(eSord, (s) => objK));
      Add(() => Queryable.ThenBy(qSord, (s) => objK, objKComp), () => Enumerable.ThenBy(eSord, (s) => objK, objKComp));
      Add(() => Queryable.ThenByDescending(qSord, (s) => objK), () => Enumerable.ThenByDescending(eSord, (s) => objK));
      Add(() => Queryable.ThenByDescending(qSord, (s) => objK, objKComp), () => Enumerable.ThenByDescending(eSord, (s) => objK, objKComp));
      // Union
      Add(() => Queryable.Union(qS, qS), () => Enumerable.Union(eS, eS));
      Add(() => Queryable.Union(qS, qS, objSComp), () => Enumerable.Union(eS, eS, objSComp));
      // Where
      Add(() => Queryable.Where(qS, (s) => true), () => Enumerable.Where(eS, (s) => true));
      Add(() => Queryable.Where(qS, (s, i) => true), () => Enumerable.Where(eS, (s, i) => true));
      // Zip
      Add(() => Queryable.Zip(qS, eInner, (s, ss) => objEl), () => Enumerable.Zip(eS, eInner, (s, ss) => objEl));
    }

    private static void Add<T1, T2>(Expression<Func<T1>> queryExpr, Expression<Func<T2>> enumExpr) {
      var qMethod = ExtractMethod(queryExpr);
      var eMethod = ExtractMethod(enumExpr);

      //Some checks against copy/paste errors in Init method
      Util.Check(qMethod.Name == eMethod.Name, "Queryable method name {0} does not match Enumerable method name {1}", qMethod.Name, eMethod.Name);
      var qParams = qMethod.GetParameters();
      var eParams = eMethod.GetParameters(); 
      Util.Check(qParams.Length == eParams.Length, 
          "Param count for method {0} does not match: Queryable param count: {1}, Enumerable param count: {2}", 
          qMethod.Name, qParams.Length, eParams.Length);
      Util.Check(!_map.ContainsKey(qMethod), "Map already contains Queryable method {0}, param count {1}.", qMethod.Name, qParams.Length);
      Util.Check(!_enumerableMethods.Contains(eMethod), "Map already contains Enumerable method {0}, param count {1}.", eMethod.Name, eParams.Length);
      
      // Add key/value to map and hashset of enumerable methods
      _map.Add(qMethod, eMethod);
      _enumerableMethods.Add(eMethod); 
    }

    private static MethodInfo ExtractMethod(Expression call) {
      var lambda = call as LambdaExpression;
      var methodExpr = lambda.Body as MethodCallExpression;
      var method = methodExpr.Method;
      if (method.IsGenericMethod) method = method.GetGenericMethodDefinition();
      return method; 
    }

    // Produces a dummy argument for some method references that use queryables of primitive types (Average, Sum)
    private static IQueryable<T> QueryOf<T>() {
      return (new T[] { default(T) }).AsQueryable();
    }
    #endregion

    #region Dummy classes used as type arguments in initialization
    class TSource { }
    class TSource2 { }
    class TKey { }
    class TElement { }
    class TKeyComparer : IComparer<TKey>, IEqualityComparer<TKey> {
      public int Compare(TKey x, TKey y) { return 0; }
      public bool Equals(TKey x, TKey y) { return true; }
      public int GetHashCode(TKey obj) { return 0; }
    }
    class TSourceComparer : IEqualityComparer<TSource> {
      public bool Equals(TSource x, TSource y) { return true; }
      public int GetHashCode(TSource obj) { return 0; }
    }
    #endregion

  }//class
}
