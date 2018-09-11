using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Vita.Entities.Runtime {

  public static class SearchHelper {

    public static SearchResults<TResult> ExecuteSearchImpl<TEntity, TResult>(
                  EntitySession session,
                  Expression<Func<TEntity, bool>> where,
                  SearchParams searchParams,
                  Func<TEntity, TResult> converter,
                  Expression<Func<TEntity, object>> include = null,
                  QueryOptions options = QueryOptions.ForceIgnoreCase,
                  Dictionary<string, string> nameMapping = null)
        where TEntity : class {
      var tempResults = ExecuteSearchImpl<TEntity>(session, where, searchParams, include, options, nameMapping);
      if (converter == null) {
        Util.Check(typeof(TEntity) == typeof(TResult), "ExecuteSearch - if result type is different from entity type, converter method must be provided.");
        return (SearchResults<TResult>)(object)tempResults;
      }
      var results = tempResults.Convert(converter);
      return results;
    }

    public static SearchResults<TEntity> ExecuteSearchImpl<TEntity>(
                  EntitySession session,
                  Expression<Func<TEntity, bool>> where,
                  SearchParams searchParams,
                  Expression<Func<TEntity, object>> include = null,
                  QueryOptions options = QueryOptions.ForceIgnoreCase,
                  Dictionary<string, string> nameMapping = null) where TEntity : class {
      try {
        var baseQuery = session.EntitySet<TEntity>().Where(where).WithOptions(options); //this will be reused by Count query
        //add include, order by, skip, take 
        var incQuery = include == null ? baseQuery : baseQuery.Include<TEntity>(include);
        var orderedQuery = string.IsNullOrEmpty(searchParams.OrderBy) ? incQuery : incQuery.OrderBy(searchParams.OrderBy, nameMapping);
        // Add Skip, Take; we return '+1' rows, to check if there are any more; if not, we do not need to query total
        var takePlusOne = searchParams.Take + 1;
        var pageQuery = orderedQuery.Skip(searchParams.Skip).Take(takePlusOne);
        var rows = pageQuery.ToList();
        if (rows.Count < takePlusOne)
          //We see the last row, we do not need to run total query
          return new SearchResults<TEntity>() { Results = rows, TotalCount = searchParams.Skip + rows.Count };
        // we received more than Take number of rows; it means we need to run TotalCount
        //save main query command, and restore it after total query; in debugging main query is more interesting than total query
        var queryCmd = session.GetLastCommand();
        var totalCount = baseQuery.Count(); //use baseQuery here, without OrderBy, Skip, Take
        session.SetLastCommand(queryCmd); //restore main query command
        rows.RemoveAt(rows.Count - 1); //remove last extra row
        var results = new SearchResults<TEntity>() { Results = rows, TotalCount = totalCount };
        return results;
      } catch (Exception ex) {
        session.Context.App.AppEvents.OnError(session.Context, ex);
        throw;
      }
    }

  } //class
}
