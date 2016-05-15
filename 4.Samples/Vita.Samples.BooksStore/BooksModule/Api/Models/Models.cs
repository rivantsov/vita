using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Samples.BookStore.Api {

  [DebuggerDisplay("{Title}")]
  public class Book {
    public Guid Id ;
    public Publisher Publisher ;
    public string Title ; //uses default length
    public DateTime? PublishedOn ;
    public BookCategory Category ;
    public BookEdition Editions ;
    public Decimal Price ;
    public List<Author> Authors ;
    public Guid? CoverImageId;
    //Details
    public string Description ;
    public string Abstract ;
    public List<BookReview> LatestReviews ; //Latest 5 reviews

  }

  [DebuggerDisplay("{Name}")]
  public class Publisher {
    public Guid Id ;
    public string Name ;
  }

  [DebuggerDisplay("{LastName}, {FirstName}")]
  public class Author {
    public Guid Id ;
    public string FirstName ;
    public string LastName ;
    //Details
    public string Bio ;
  }

  [DebuggerDisplay("{UserName}:{Caption}")]
  public class BookReview {
    public Guid Id;
    public DateTime CreatedOn ;
    public Guid BookId ;
    public string UserName ;
    public int Rating ;
    public string Caption ;
    //Details
    public string Review ;
  }

  [DebuggerDisplay("{UserName}, {CreatedOn}, Total: {Total}")]
  public class BookOrder {
    public Guid Id;
    public Guid UserId;
    public string UserName; 
    public DateTime CreatedOn;
    public Decimal Total;
    public OrderStatus Status; 
    //Details
    public List<BookOrderItem> Items;
  }

  [DebuggerDisplay("{Book.Title}")]
  public class BookOrderItem {
    public Guid Id;
    public Guid OrderId; 
    public Book Book;
    public int Quantity;
  }

  [DebuggerDisplay("{UserName}")]
  public class User {
    public Guid Id;
    public string UserName;
    public string DisplayName;
  }

  [DebuggerDisplay("{UserName}")]
  public class UserSignup {
    public string UserName;
    public string DisplayName;
    public string Password; 
  }

}
