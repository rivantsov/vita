using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQLServer {

  public class Paging {
    public string OrderBy;
    public int Skip;
    public int Take;
  }

  public class BookSearch {
    /// <summary>Title start substring to search for.</summary>
    public string Title;
    public string Categories; //comma-delimited string of values
    public double? MaxPrice;
    public string Publisher;
    public DateTime? PublishedAfter;
    public DateTime? PublishedBefore;
    public string AuthorLastName;
  }

  public class AuthorSearch {
    public string LastName;
    public string BookTitle;
    public BookCategory? Category;
    public string Publisher;
  }
}
