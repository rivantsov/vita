using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities {
  //Search helper methods
  public static partial class EntitySessionExtensions {

    // Creates initial expression for WHERE predicate
    /// <summary>Creates an initial predicate expression for dynamically built queries.</summary>
    /// <typeparam name="T">The entity type to be searched.</typeparam>
    /// <param name="session">Entity session instance.</param>
    /// <param name="value">Initial value of the predicate.</param>
    /// <returns>An expression representing a predicate, a seed for a WHERE clause.</returns>
    /// <remarks>Use extension methods for a resulting predicate (ex: .And, .Or, etc) to dynamically construct a WHERE 
    /// condition for a search query.</remarks>
    public static Expression<Func<T, bool>> NewPredicate<T>(this IEntitySession session, bool value = true) {
      if (value)
        return f => true;
      else
        return f => false;
    }

    /// <summary>Executes a search query for a given WHERE condition, and returns results object which contains the resulting rows (page)
    /// and total count of rows for a search condition. </summary>
    /// <typeparam name="TEntity">Root entity type.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="where">Combined WHERE condition for a query.</param>
    /// <param name="searchParams">Search parameters object containing extra options for the query: order by, skip, take.</param>
    /// <param name="include">Include expression.</param>
    /// <param name="nameMapping">A name mapping dictionary, to map names in order-by expression to actual properties of the entity.</param>
    /// <param name="options">Query options.</param>
    /// <returns>An instance of the <c>SearchResults</c> class, with selected rows and total count for the query condition.</returns>
    public static SearchResults<TEntity> ExecuteSearch<TEntity>(
                  this IEntitySession session,
                  Expression<Func<TEntity, bool>> where,
                  SearchParams searchParams,
                  Expression<Func<TEntity, object>> include = null,
                  QueryOptions options = QueryOptions.ForceIgnoreCase,
                  Dictionary<string, string> nameMapping = null)
        where TEntity : class {
      return ExecuteSearch<TEntity, TEntity>(session, where, searchParams, null, include, options, nameMapping);
    }

    /// <summary>Executes a search query for a given WHERE condition, and returns results object which contains the resulting rows (page)
    /// and total count of rows for a search condition. The resulting rows are converted to a new type using the converter function. </summary>
    /// <typeparam name="TEntity">Root entity type.</typeparam>
    /// <typeparam name="TResult">Type of result rows.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="where">Combined WHERE condition for a query.</param>
    /// <param name="searchParams">Search parameters object containing extra options for the query: order by, skip, take.</param>
    /// <param name="converter">A converter function from entity type to result row type.</param>
    /// <param name="include">Include expression.</param>
    /// <param name="options">Query options.</param>
    /// <param name="nameMapping">A name mapping dictionary, to map names in order-by expression to actual properties of the entity.</param>
    /// <returns>An instance of the <c>SearchResults</c> class, with selected rows and total count for the query condition.</returns>
    public static SearchResults<TResult> ExecuteSearch<TEntity, TResult>(
                  this IEntitySession session,
                  Expression<Func<TEntity, bool>> where,
                  SearchParams searchParams,
                  Func<TEntity, TResult> converter = null,
                  Expression<Func<TEntity, object>> include = null,
                  QueryOptions options = QueryOptions.ForceIgnoreCase,
                  Dictionary<string, string> nameMapping = null)
        where TEntity : class {
      var results = SearchHelper.ExecuteSearchImpl<TEntity, TResult>( 
                      (EntitySession) session, where, searchParams, converter, include, options, nameMapping);
      return results; 
    }

    /// <summary>Converts SearchResults over one row type to results of other underlying type using a converter method.</summary>
    /// <typeparam name="T1">Original search results row type.</typeparam>
    /// <typeparam name="T2">Target result row type.</typeparam>
    /// <param name="results">Original search results object.</param>
    /// <param name="converter">Row converter.</param>
    /// <returns>Search results object over new row type.</returns>
    public static SearchResults<T2> Convert<T1, T2>(this SearchResults<T1> results, Func<T1, T2> converter) {
      var newResults = new SearchResults<T2>() { TotalCount = results.TotalCount };
      foreach (var r in results.Results)
        newResults.Results.Add(converter(r));
      return newResults;
    }

  
  }//class
}
