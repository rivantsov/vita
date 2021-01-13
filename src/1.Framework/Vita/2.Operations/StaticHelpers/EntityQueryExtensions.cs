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
        query = isDesc ? query.OrderByDescending(lambda) : query.OrderBy(lambda);
      }
      return query;
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


    /// <summary>Creates default SearchParams object if instance is null. Also sets default value for Take property. </summary>
    /// <typeparam name="T">Params class type - must derive from SearchParams class.</typeparam>
    /// <param name="searchParams">Search parameters, must be derived from SearchParams class.</param>
    /// <param name="take">Default value to set for Take property if it is 0.</param>
    /// <param name="defaultOrderBy">Default value for order-by parameter.</param>
    /// <param name="maxRows">Max rows to return, a limit to Take parameter. </param>
    /// <returns>Existing or new SearchParams-derived object.</returns>
    /// <remarks>User this method in Search methods in Web API controller for input parameter marked with [FromUri] attribute
    /// that is a container for multiple parameters in URL.</remarks>
    public static T DefaultIfNull<T>(this T searchParams, int take = 10, string defaultOrderBy = null, int maxRows = 100)
            where T : SearchParams, new() {
      if(searchParams == null)
        searchParams = new T();
      if(searchParams.Take == 0)
        searchParams.Take = take;
      if(string.IsNullOrEmpty(searchParams.OrderBy))
        searchParams.OrderBy = defaultOrderBy;
      if(searchParams.Take > maxRows)
        searchParams.Take = maxRows;
      return searchParams;
    }

    /// <summary>Executes a SQL insert statement based on LINQ query. </summary>
    /// <typeparam name="TEntity">Entity type for entity(ies) being inserted.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="baseQuery">Base LINQ query.</param>
    /// <returns>Number of entities inserted.</returns>
    /// <remarks>
    /// <para>The base query must return an anonymous type with properties matching the names of the entity being inserted. 
    /// The assigned expressions (right side) in anonymous object initializer are the values for columns in inserted rows.
    /// For foreign key columns (entity references) you should provide a property with a name matching the key column (ex: Book_Id), 
    /// and assign it the value of the column, ex: ' Book_id = bk.Id, '. Using entity reference directly is not supported (yet).
    /// </para><para>
    /// About generating new GUID-s for primary key values in new rows: use Guid.NewGuid() function as a value for 
    /// primary key column (Id); the system will translate it into server-specific function. SQLite does not have 
    /// a function for this, so INSERT statements for tables with GUID-type primary keys are not supported 
    /// for this database type. 
    /// </para>
    /// </remarks> 
    public static int ExecuteInsert<TEntity>(this IEntitySession session, IQueryable baseQuery) {
      Util.CheckParam(session, nameof(session));
      var entSession = (EntitySession)session;
      return entSession.ExecuteLinqNonQuery<TEntity>(baseQuery, LinqOperation.Insert);
    }

    /// <summary>Executes a SQL update statement based on LINQ query. </summary>
    /// <typeparam name="TEntity">Entity type for entity(ies) being updated.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="baseQuery">Base LINQ query.</param>
    /// <returns>Number of entities updated.</returns>
    /// <remarks>
    /// <para>The base query must return an anonymous type with properties matching the names of the entity being updated; 
    /// the assigned expressions (right side) in anonymous object initializer are the 'new' values for the left-side properties.
    /// For foreign key columns (entity references) you should provide a property with a name matching the key column (ex: Book_Id), 
    /// and assign it the value of the column, ex: ' Book_id = bk.Id, '. Using entity reference directly is not supported (yet).
    /// </para><para>
    /// For simple updates (not using values from other tables/entities) the output type does not need to include primary key property.
    /// The WHERE clause of LINQ expression will be translated into WHERE clause of the update SQL command. </para><para>
    /// For complex updates using values from other entities the anonymous type must include the property matching the primary key 
    /// of the entity. The primary key value will not be updated, but will be used in the WHERE clause of the UPDATE statement 
    /// for filtering rows for update.
    /// </para>
    /// </remarks> 
    public static int ExecuteUpdate<TEntity>(this IEntitySession session, IQueryable baseQuery) {
      Util.CheckParam(session, nameof(session));
      var entSession = (EntitySession)session;
      return entSession.ExecuteLinqNonQuery<TEntity>(baseQuery, LinqOperation.Update);
    }

    /// <summary>Executes a SQL delete statement based on LINQ query. </summary>
    /// <typeparam name="TEntity">Entity type for entity(ies) to delete.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="baseQuery">Base LINQ query.</param>
    /// <returns>Number of entities deleted.</returns>
    /// <remarks>
    /// <para>The base query must return an anonymous type with a single property matching the primary key.
    /// </para>
    /// <para>For simple deletes (not using values from other tables/entities) the output key value is ignored and DELETE statement
    /// simply includes the WHERE clause from the base query.
    /// For complex deletes thtat use values from other entities/tables the primary key values identify the records to be deleted.
    /// </para>
    /// </remarks> 
    public static int ExecuteDelete<TEntity>(this IEntitySession session, IQueryable baseQuery) {
      Util.CheckParam(session, nameof(session));
      var entSession = (EntitySession)session;
      return entSession.ExecuteLinqNonQuery<TEntity>(baseQuery, LinqOperation.Delete);
    }

    public static void ScheduleInsert<TEntity>(this IEntitySession session, IQueryable query,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      var entSession = (EntitySession)session;
      entSession.ScheduleLinqNonQuery<TEntity>(query, LinqOperation.Insert, schedule);
    }

    public static void ScheduleUpdate<TEntity>(this IEntitySession session, IQueryable query,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      var entSession = (EntitySession)session;
      entSession.ScheduleLinqNonQuery<TEntity>(query, LinqOperation.Update, schedule);
    }

    public static void ScheduleDelete<TEntity>(this IEntitySession session, IQueryable query,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      var entSession = (EntitySession)session;
      entSession.ScheduleLinqNonQuery<TEntity>(query, LinqOperation.Delete, schedule);
    }

  }//class
}
