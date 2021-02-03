using System;
using System.Collections.Generic;
using System.Diagnostics;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQLServer {

  [DebuggerDisplay("{Title}")]
  public class Book {
    public Guid Id;
    public Publisher Publisher;
    public string Title;
    public IList<Author> Authors;
    public DateTime? PublishedOn;
    public BookCategory Category;
    public BookEdition Editions;
    public Decimal Price;
    [Null] public string CoverImageUrl;
    [Null] public User Editor; 
    //Details
    [Null] public string Description;
    [Null] public string Abstract;

    /// <summary>Books reviews </summary>
    [GraphQLName("reviews")]
    public List<BookReview> GetBookReviews([Null]Paging paging = null) { return default; } 
  }

  [DebuggerDisplay("{Name}")]
  public class Publisher {
    public Guid Id;
    public string Name;
    public IList<Book> Books;
  }

  [DebuggerDisplay("{LastName}, {FirstName}")]
  public class Author {
    public Guid Id;
    public string FirstName;
    public string LastName;
    public string Bio;
    public string FullName; 
    public IList<Book> Books;
  }

  [DebuggerDisplay("{Caption??Id}")]
  public class BookReview {
    public Guid Id;
    public DateTime CreatedOn;
    public Book Book;
    public User User;
    public int Rating;
    public string Caption;
    public string Review;
  }

  [DebuggerDisplay("{Caption}")]
  public class BookReviewInput {
    public Guid BookId;
    public Guid UserId;
    public int Rating;
    public string Caption;
    public string Review;
  }


  [DebuggerDisplay("{UserName}, {CreatedOn}, Total: {Total}")]
  public class BookOrder {
    public Guid Id;
    public User User;
    public DateTime CreatedOn;
    public Decimal Total;
    public OrderStatus Status;
    public string TrackingNumber;
    public List<BookOrderLine> Lines;
  }

  [DebuggerDisplay("{Book.Title}")]
  public class BookOrderLine {
    public Guid Id;
    public BookOrder Order;
    public Book Book;
    public int Quantity;
  }

  [DebuggerDisplay("{UserName}")]
  public class User {
    public Guid Id;
    public string UserName;
    public string DisplayName;
    public UserType UserType;
    /// <summary>Books reviews </summary>
    [GraphQLName("reviews")]
    public List<BookReview> GetUserReviews(Paging paging = null) { return default; }
    [GraphQLName("orders")]
    public List<BookOrder> GetUserOrders(Paging paging = null) { return default; }
  }

  public class Paging {
    [Null] public string OrderBy;
    public int? Skip;
    public int? Take;
  }

  public class BookSearchInput {
    /// <summary>Title start substring to search for.</summary>
    [Null] public string Title;
    [Null] public BookCategory[] Categories;
    public BookEdition? Editions;
    public double? MaxPrice;
    [Null] public string Publisher;
    public DateTime? PublishedAfter;
    public DateTime? PublishedBefore;
    [Null] public string AuthorLastName;
  }

}
