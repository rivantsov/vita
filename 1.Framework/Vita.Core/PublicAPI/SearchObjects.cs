using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {
  /// <summary>Base class for search parameters containers. 
  /// Use a derived class as a parameter for search method in controllers with [FromUrl] attribute.
  /// For classic WebApi ApiControllers use it with [FromUri] attribute.</summary>
  /// <remarks>
  /// <para>EntitySessionExtensions.CreateSearch method expects an instance of SearchParams sub-class.  </para>
  /// <para> Use WebHelper.DefaultIfNull extension method in API controller search method to create 
  /// a default instance if no parameters were provided in Search API call. </para>
  /// <para>Important: Use properties in the derived class, not fields - fields do not work with Web Api [FromUri] attribute.</para>
  /// </remarks>
  public class SearchParams {
    /// <summary>List of columns with optional 'desc' spec. </summary>
    /// <remarks>Use comma or semicolon to separate properties; use dash (-) or colon to add 'desc' flag (dash is more URL-friendly).
    /// You can also map simple names to more complex names (or dotted property chains), by providing 
    /// mapping dictionary to SearchHelper.CreateSearch method. </remarks>
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
  /// <remarks>An instance of the SearchResults type is returned by the <c>session.ExecuteSearch</c>  extension methods. </remarks>
  public class SearchResults<T> {
    /// <summary>Total row count for a search criteria without skip/take restriction.</summary>
    public long TotalCount;
    /// <summary>Actual results page.</summary>
    public IList<T> Results = new List<T>();
  }

}//ns
