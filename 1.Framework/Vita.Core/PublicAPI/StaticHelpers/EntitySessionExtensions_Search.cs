using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

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

    /// <summary>
    /// Creates a search query expression from a predicate and a <c>SearchParams</c> instance, with optional property mapping for ORDER BY clause.
    /// </summary>
    /// <typeparam name="T">Root entity type to be searched.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="where">Search condition.</param>
    /// <param name="searchParams">Search options to be added to the result query: Order-By, Skip, Take. </param>
    /// <param name="nameMapping">Optional, mapping of names in OrderBy list (in searchParams) to actual entity property names. </param>
    /// <param name="options">Optional, search query options.</param>
    /// <returns>Combined search query expression.</returns>    
    public static IQueryable<T> CreateSearch<T>(this IEntitySession session,
                  Expression<Func<T, bool>> where,
                  SearchParams searchParams,
                  Dictionary<string, string> nameMapping = null, 
                  QueryOptions options = QueryOptions.ForceIgnoreCase) where T : class {
      var entQuery = session.EntitySet<T>().Where(where).WithOptions(options);
      if (!string.IsNullOrEmpty(searchParams.OrderBy))
        entQuery = entQuery.OrderBy(searchParams.OrderBy, nameMapping);
      // Add Skip, Take
      var result = entQuery.Skip(searchParams.Skip).Take(searchParams.Take);
      return result;
    }

    /// <summary>Executes a search query for a given WHERE condition, and returns results object which contains the resulting rows (page)
    /// and total count of rows for a search condition.</summary>
    /// <typeparam name="TEntity">Root entity type.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="where">Combined WHERE condition for a query.</param>
    /// <param name="searchParams">Search parameters object containing extra options for the query: order by, skip, take.</param>
    /// <param name="include">Include expression.</param>
    /// <param name="nameMapping">A name mapping dictionary, to map names in order-by expression to actual properties of the entity.</param>
    /// <param name="options">Optional, search query options.</param>
    /// <returns>An instance of the <c>SearchResults</c> class, with selected rows and total count for the query condition.</returns>
    public static SearchResults<TEntity> ExecuteSearch<TEntity>(
                  this IEntitySession session,
                  Expression<Func<TEntity, bool>> where,
                  SearchParams searchParams,
                  Expression<Func<TEntity, object>> include = null,
                  Dictionary<string, string> nameMapping = null,
                  QueryOptions options = QueryOptions.ForceIgnoreCase) where TEntity : class {
      var baseQuery = session.EntitySet<TEntity>().Where(where).WithOptions(options); //this will be reused by Count query
      //add include, order by, skip, take 
      var incQuery = include == null ? baseQuery : baseQuery.Include<TEntity>(include); 
      var orderedQuery = string.IsNullOrEmpty(searchParams.OrderBy) ? incQuery : incQuery.OrderBy(searchParams.OrderBy, nameMapping);
      // Add Skip, Take; we return '+1' rows, to check if there are any more; if not, we do not need to query total
      var takePlusOne = searchParams.Take + 1; 
      var pageQuery = orderedQuery.Skip(searchParams.Skip).Take(takePlusOne);
      var rows = pageQuery.ToList();
      if(rows.Count < takePlusOne)
        //We see the last row, we do not need to run total query
        return new SearchResults<TEntity>() { Results = rows, TotalCount = searchParams.Skip + rows.Count };
      // we received more than Take number of rows; it means we need to run TotalCount
      //save main query command, and restore it after total query; in debugging main query is more interesting than total query
      var queryCmd = session.GetLastCommand(); 
      var totalCount = baseQuery.Count(); //use baseQuery here, without OrderBy, Skip, Take
      session.SetLastCommand(queryCmd); //restore main query command
      rows.RemoveAt(rows.Count - 1); //remove last extra row
      var results = new SearchResults<TEntity>() {Results = rows, TotalCount = totalCount }; 
      return results;
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
    /// <param name="nameMapping">A name mapping dictionary, to map names in order-by expression to actual properties of the entity.</param>
    /// <returns>An instance of the <c>SearchResults</c> class, with selected rows and total count for the query condition.</returns>
    public static SearchResults<TResult> ExecuteSearch<TEntity, TResult>(
                  this IEntitySession session,
                  Expression<Func<TEntity, bool>> where,
                  SearchParams searchParams,
                  Func<TEntity, TResult> converter,
                  Expression<Func<TEntity, object>> include = null,
                  Dictionary<string, string> nameMapping = null)
        where TEntity : class {
      var tempResults = ExecuteSearch(session, where, searchParams, include, nameMapping);
      var results = tempResults.Convert(converter);
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
