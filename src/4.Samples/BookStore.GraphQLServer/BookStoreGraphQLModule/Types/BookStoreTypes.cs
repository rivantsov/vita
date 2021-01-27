﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQLServer {

  [DebuggerDisplay("{Title} by {Publisher.Name}")]
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
    public List<BookReview> GetBookReviews(Paging paging = null) { return default; } 
  }

  [DebuggerDisplay("{Name}")]
  public class Publisher {
    public Guid Id;
    public string Name;
    [GraphQLName("books")]
    public IList<Book> GetPublisherBooks(Paging paging = null) { return default; }
  }

  [DebuggerDisplay("{LastName}, {FirstName}")]
  public class Author {
    public Guid Id;
    public string FirstName;
    public string LastName;
    public string Bio;
    [GraphQLName("books")]
    public IList<Book> GetBooksByAuthor(Paging paging = null) { return default; }
  }

  [DebuggerDisplay("{Caption}")]
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
  }

  /* Not used yet
  [DebuggerDisplay("{UserName}")]
  public class UserSignup {
    public string UserName;
    public string DisplayName;
    public string Password;
  }

  [DebuggerDisplay("{UserName}")]
  public class UserLogin {
    public string UserName;
    public string Password;
  }

  [DebuggerDisplay("{UserName}")]
  public class LoginResponse {
    public string Token;
    public User User;
  }
  */
}
