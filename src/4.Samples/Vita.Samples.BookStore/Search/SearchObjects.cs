using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Samples.BookStore {
  // Containers for search parameters, used as '[FromUri]' parameter in search API methods. 
  // Important - use properties, fields do not work for FromUri controller method parameter. 
  // Base SearchParams defines OrderBy, Skip, Take - common search parameters
  // CreateSearch, ExecuteSearch helper methods expect SearchForm-derived object 
  public class BookSearch : SearchParams {
    /// <summary>Title start substring to search for.</summary>
    public string Title { get; set; }
    public string Categories { get; set; } //comma-delimited string of values
    public double? MaxPrice { get; set; }
    public string Publisher { get; set; }
    public DateTime? PublishedAfter { get; set; }
    public DateTime? PublishedBefore { get; set; }
    public string AuthorLastName { get; set; }
  }

  public class AuthorSearch : SearchParams {
    public string LastName { get; set; }
    public string BookTitle { get; set; }
    // public int? MinRating { get; set; } //might add in the future, when book has rating (average of all reviews)
    public BookCategory? Category { get; set; }
    public string Publisher { get; set; }
  }

  public class PublishersSearch : SearchParams {
    public string Name { get; set; }
  }

  public class ReviewSearch : SearchParams {
    public Guid? UserId { get; set; }
    public string UserName { get; set; }
    public Guid? BookId { get; set; }
    public BookCategory? BookCategory {get; set;}
    public Guid? PublisherId { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
    public string Title { get; set; }
  }

  public class BookOrderSearch : SearchParams {
    public Guid? UserId { get; set; } //if user type is Customer, this value is ignored and UserId is set from current user Id
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public decimal? MinTotal { get; set; }
    public string Publisher { get; set; }
    public Guid? BookId { get; set; }
    public string BookTitle { get; set; }
    public BookCategory? BookCategory { get; set; }
  }

}
