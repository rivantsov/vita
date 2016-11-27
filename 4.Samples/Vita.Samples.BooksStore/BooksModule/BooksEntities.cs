using System;
using System.Collections.Generic;
using System.ComponentModel;

using Vita.Entities;
using Vita.Modules;

namespace Vita.Samples.BookStore {

  //Entities for Books module
  // Note that Book and Author have Paged attribute - meaning they are paged in database (in stored procedure)
  // Publisher does not have this attribute, we expect it to be a small table. We can still read/query it with 
  // skip/take parameters, but paging is performed on the client: all records are loaded and then code extracts the span of requested records. 
  [Entity, Paged, OrderBy("Title"), Validate(typeof(BooksModule), nameof(BooksModule.ValidateBook))]
  [Display("{Title}, {Editions}, {Publisher.Name}")]
  [Description("Represents a book.")]
  public interface IBook {

    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Utc, Auto(AutoType.CreatedOn)]
    DateTime CreatedOn { get; }

    [Description("Book publisher.")] // We add some decriptions to book properties - they will appear on Web service help page.
    IPublisher Publisher { get; set; }
    
    [Description("Book title."), Size(120)] //explicitly set size
    string Title { get; set; } 
    
    [Size(Sizes.Description), Nullable, Description("Book description.")]
    string Description { get; set; }

    [Description("Publication date.")]
    DateTime? PublishedOn { get; set; }

    [Nullable]
    IImage CoverImage { get; set; }

    [Unlimited, Nullable, Description("Book abstract.")]
    string Abstract { get; set; }
    
    [Description("Book category.")]
    BookCategory Category { get; set; }

    [Description("Available editions.")]
    BookEdition Editions { get; set; }

    [Description("Book price.")]
    Decimal Price { get; set; }

    [ManyToMany(typeof(IBookAuthor)), Description("Book authors.")]
    IList<IAuthor> Authors { get; }

    [Nullable]
    IUser Editor { get; set; }

     // Just to play with model - uncomment this to see column and index appear in the database
    // ISBN should be Unique, but we make it nullable so for existing records (if any) it can be set to null
    //[Nullable, Size(40), Unique(Filter = "{Isbn} IS NOT NULL")]
    //string Isbn { get; set; }

  }

  [Entity, OrderBy("Name"), Description("Represents a publisher.")]
  [Display("{Name}")]
  public interface IPublisher {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Size(Sizes.Name)]
    string Name { get; set; }
    [OrderBy("PublishedOn:DESC")]
    IList<IBook> Books { get; }

    // Just to demo list filters
    [OneToMany(Filter=" {Category} = 1")] //fiction
    IList<IBook> FictionBooks { get; }
  }

  [Entity, Paged, OrderBy("LastName,FirstName"), Description("Represents an author.")]
  [Display("{LastName}, {FirstName}")]
  public interface IAuthor {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(Sizes.Name), Nullable]
    string FirstName { get; set; }
    
    [Size(Sizes.Name)]
    string LastName { get; set; }

    [Nullable]
    IImage Photo { get; set; }

    [Unlimited, Nullable]
    string Bio { get; set; }
    
    [ManyToMany(typeof(IBookAuthor)), OrderBy("PublishedOn:DESC")]
    IList<IBook> Books { get; }
    
    [Nullable]
    IUser User { get; set; } //author might be a user

    [Computed(typeof(BooksModule), "GetFullName"), DependsOn("FirstName,LastName")] //DependsOn is optional, used for auto PropertyChanged firing
    string FullName { get; }
  }

  // Note: we set CascadeDelete on reference to Book, but not Author. 
  // We can always delete a book, and the link to the author will be deleted automatically.
  // But we cannot delete an Author if there are any books in the system by this author. 
  [Entity,  PrimaryKey("Book, Author", IsClustered=true)] //intentionally with space, to ensure it is handled OK
  [Display("{Book}-{Author}")]
  public interface IBookAuthor {
    [CascadeDelete]
    IBook Book { get; set; }
    IAuthor Author { get; set; }
  }

  [Entity, OrderBy("UserName")]
  [Display("{DisplayName}")]
  public interface IUser {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(Sizes.UserName, 30)]
    string UserName { get; set; }

    [HashFor("UserName"), Index]
    int UserNameHash { get; }

    [Size(Sizes.UserName)]
    string DisplayName { get; set; }
    
    UserType Type { get; set; } //might be combination of several type flags
    bool IsActive { get; set; }

    //For user who is editor. Kinda silly, just testing here one special case - list property with nullable back reference (book->editor)
    IList<IBook> BooksEdited { get; }
  }

  [Entity, OrderBy("CreatedOn"), ClusteredIndex("CreatedOn,User,Id")]
  [Display(typeof(BooksModule), "GetOrderDisplay")]
  public interface IBookOrder {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }

    [Auto(AutoType.UpdatedOn), Utc]
    DateTime UpdatedOn { get; }

    [NoUpdate]
    IUser User { get; set; }

    decimal Total { get; set; }

    OrderStatus Status { get; set; }

    [PersistOrderIn("LineNumber")]
    IList<IBookOrderLine> Lines { get; }

    //DependsOn is optional, used for auto PropertyChanged firing
    [Computed(typeof(BooksModule), "GetOrderSummary"), DependsOn("User,Total,CreatedOn")] 
    string Summary { get; }

  }

  [Entity, ClusteredIndex("Order,Id")] //, OrderBy("LineNumber")]
  public interface IBookOrderLine {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [NoUpdate, CascadeDelete] //Delete all lines when order is deleted
    [PropagageUpdatedOn] //update Order.UpdatedOn whenever order line is updated/deleted
    IBookOrder Order { get; set; }
    int LineNumber { get; set; } //automatically maintained line order - see IBookOrder.Lines property
    // Making it byte to test Postgres feature. See comments at the end of the file. 
    byte Quantity { get; set; }
    IBook Book { get; set; }
    Decimal Price { get; set; } //captures price at the time the order was created
  }

  //We track history for book review
  [Entity, OrderBy("CreatedOn"), ClusteredIndex("Book,CreatedOn,Id"), Modules.DataHistory.KeepHistory]
  public interface IBookReview {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }

    [NoUpdate]
    IBook Book { get; set; }
    
    // Anonymous user does not have any permissions for other User records, only his own. 
    // But when reading reviews, he must be able to see the display name of review author. 
    // So we grant permission to read DisplayName through reference - meaning that if User record
    // was retrieved thru review.User property, DisplayName is available to read.
    [NoUpdate, GrantAccessAttribute("DisplayName")] 
    IUser User { get; set; }

    //1 .. 5
    int Rating { get; set; }
    
    [Size(100)]
    string Caption { get; set; }
    [Unlimited]
    string Review { get; set; }
  }

  [Entity, OrderBy("CreatedOn"), ClusteredIndex("CreatedOn,Id")]
  [Display("{PromoCode}: {DiscountPerc}%")]
  // [Display("{PromoCode}: {DiscountPerc.Blah}%")] //with error - should be swallowed and ToString() displays error message
  public interface ICoupon {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }

    DateTime ExpiresOn { get; set; }

    [Size(20), Index]
    string PromoCode { get; set; }

    double DiscountPerc { get; set; }

    DateTime? AppliedOn { get; set; }
  }

  // Views ----------------------------------------
  public interface IBookSales {
    [UniqueClusteredIndex]
    Guid Id { get; set; }
    [Index]
    string Title { get; set; }
    string Publisher { get; set; }
    int Count { get; set; }
    Decimal Total { get; set; }
  }

  public interface IBookSales2 {
    Guid Id { get; set; }
    string Title { get; set; }
    string Publisher { get; set; }
    int Count { get; set; }
    Decimal Total { get; set; }
  }

  public interface IFictionBook : IBook { }

  public interface IAuthorUser {
    string FirstName { get; }
    string LastName { get; }
    string UserName { get; }
    UserType? UserType { get; }
  }

  [Entity]
  public interface IImage {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    ImageType Type { get; set; }
    [Size(Sizes.Name)]
    string Name { get; set; }
    [Size(50)]
    string MediaType { get; set; } // ex: 'image/jpeg'
    [Unlimited]
    byte[] Data { get; set; }
  }

  // Notes on IBookOrderLine.Quantity (byte) 
  // we make it byte to test some behavior in Postgres; Postgres function overloading resolution 
  // gets into error in batch mode - fails to find overload for a CRUD function call. It assumes 
  // that literal number in parameter to batched proc call (ex: 5) is Int and fails match to method 
  // which expects byte. So VITA Postgres driver injects CAST to explicitly cast the number

}
