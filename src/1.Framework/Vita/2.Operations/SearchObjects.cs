using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Api;

namespace Vita.Entities {

  /// <summary>Base interface for search parameters containers. Use an implementing type as a parameter for search methods in controllers.</summary>
  /// <remarks>
  /// <para>The <c>"IEntitySession.ExecuteSearch"</c> expects an instance of ISearchParams.  </para>
  /// </remarks>
  public interface ISearchParams {
    /// <summary>List of columns with optional 'desc' spec. </summary>
    /// <remarks>Use comma or semicolon to separate properties; use dash (-) or colon to add 'desc' flag (dash is more URL-friendly).
    /// You can also map simple names to more complex names (or dotted property chains), by providing 
    /// mapping dictionary to <c>"IEntitySession.ExecuteSearch"</c> method. </remarks>
    /// <example>
    ///   firstname-desc;lastname;birthdate-desc
    /// </example>
    string OrderBy { get; set; }
    /// <summary>Number of rows to skip.</summary>
    int Skip { get; set; }
    /// <summary>Number of rows to take.</summary>
    int Take { get; set; }
  }



  /// <summary>Default implmentation of <see cref="ISearchParams"/> interface. Use a derived class as a parameter for search methods.</summary>
  /// <remarks>
  /// <para>The <c>"IEntitySession.ExecuteSearch"</c> expects an instance of SearchParams sub-class.  </para>
  /// <para> Use the <see cref="EntitySessionExtensions.DefaultIfNull"/> extension method at the beginning 
  /// of the API controller search method to create a default instance if no URL criteria were provided in Search API call,
  /// and/or to enforce (limit) maximum Take value. </para>
  /// <para>Important: Use properties in the derived class, not fields - fields do not work with Web Api [FromUri] attribute.</para>
  /// </remarks>
  public class SearchParams: ISearchParams {
    /// <summary>List of columns with optional 'desc' spec. </summary>
    /// <remarks>Use comma or semicolon to separate properties; use dash (-) or colon to add 'desc' flag (dash is more URL-friendly).
    /// You can also map simple names to more complex names (or dotted property chains), by providing 
    /// mapping dictionary to <c>"IEntitySession.ExecuteSearch"</c> method. </remarks>
    /// <example>
    ///   firstname-desc;lastname;birthdate-desc
    /// </example>
    public string OrderBy { get; set; } 
    /// <summary>Number of rows to skip.</summary>
    public int Skip { get; set; }
    /// <summary>Number of rows to take.</summary>
    public int Take { get; set; }
  }

  /// <summary>A container for search results. Holds a total row count for a query (count executed without paging
  /// arguments), and actual page of result set (according to skip/take parameters). </summary>
  /// <typeparam name="T">Row type.</typeparam>
  /// <remarks>An instance of the SearchResults type is returned by the <c>"IEntitySession.ExecuteSearch"</c> extension methods. </remarks>
  public class SearchResults<T> {
    /// <summary>Total row count for a search criteria without skip/take restriction.</summary>
    public long TotalCount;
    /// <summary>Actual results page.</summary>
    public IList<T> Results = new List<T>();
  }

}//ns
