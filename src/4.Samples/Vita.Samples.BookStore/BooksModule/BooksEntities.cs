using System;
using System.Collections.Generic;
using System.ComponentModel;

using Vita.Entities;
using Vita.Modules;
using Vita.Modules.EncryptedData;

namespace Vita.Samples.BookStore {

  //Entities for Books module
  [Entity, OrderBy("Title"), Validate(typeof(BooksModule), nameof(BooksModule.ValidateBook))]
  [ClusteredIndex("CreatedOn,Id")]
  [Display("{Title}, {Editions}, {Publisher.Name}")]
  public interface IBook {

    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Utc, Auto(AutoType.CreatedOn)]
    DateTime CreatedOn { get; }

    IPublisher Publisher { get; set; }

    [Index(IncludeMembers = "PublishedOn,Category,Publisher", Filter = "{PublishedOn} IS NOT NULL")]
    [Size(120)]
    string Title { get; set; } 
    
    [Size(Sizes.Description), Nullable]
    string Description { get; set; }

    DateTime? PublishedOn { get; set; }

    [Nullable]
    IImage CoverImage { get; set; }

    [Unlimited, Nullable]
    string Abstract { get; set; }
    
    BookCategory Category { get; set; }

    BookEdition Editions { get; set; }

    Decimal Price { get; set; }

    [Column(Precision = 20, Scale = 6)] //testing alternative precision column
    Decimal? WholeSalePrice { get; set; }

    [ManyToMany(typeof(IBookAuthor)), OrderBy("LastName,FirstName")] 
    IList<IAuthor> Authors { get; }

    [Nullable]
    IUser Editor { get; set; }

    // nullable string property to test some special queries
    [Size(10), Nullable]
    string SpecialCode { get; set; }

     // Just to play with model - uncomment this to see column and index appear in the database
    // ISBN should be Unique, but we make it nullable so for existing records (if any) it can be set to null
    [Nullable, Size(40), Unique(Filter = "{Isbn} IS NOT NULL")]
    string Isbn { get; set; }

  }

  [Entity, OrderBy("Name")]
  [Display("{Name}")]
  public interface IPublisher {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    //Alias is used in identifying the key in UniqueIndexViolation exception
    [Size(Sizes.Name), Unique(DbKeyName ="UC_PubName", Alias = "PublisherName")] 
    string Name { get; set; }

    [OrderBy("PublishedOn:DESC")]
    IList<IBook> Books { get; }

    // Disabled, Filters to be implemented in the future (maybe)
    // Just to demo list filters
    // [OneToMany(Filter=" {Category} = 1")] //fiction
    // IList<IBook> FictionBooks { get; }
  }

  [Entity, OrderBy("LastName,FirstName")]
  [ClusteredIndex("LastName,Id")] 
  [Display("Author: {LastName}, {FirstName}")]
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
    string FullName { get;} 
  }

  // Note: we set CascadeDelete on reference to Book, but not Author. 
  // We can always delete a book, and the link to the author will be deleted automatically.
  // But we cannot delete an Author if there are any books in the system by this author. 
  [Entity,  PrimaryKey("Book, Author", Clustered=true)] //intentionally with space, to ensure it is handled OK
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

  [Entity, OrderBy("CreatedOn"), ClusteredIndex("CreatedOn:DESC,User,Id")] // DESC - just for test
  [Display(typeof(BooksModule), "GetOrderDisplay")]
  public interface IBookOrder {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }

    [Auto(AutoType.UpdatedOn), Utc]
    DateTime UpdatedOn { get; }

    // we need explicit index here; indexes are auto-created on FK only if there's a list (user.Books) on other side
    [NoUpdate, Index] 
    IUser User { get; set; }

    decimal Total { get; set; }

    OrderStatus Status { get; set; }

    // testing IEncryptedData
    [Nullable]
    IEncryptedData TrackingNumber { get; set; }

    [PersistOrderIn("LineNumber")]
    IList<IBookOrderLine> Lines { get; }

    //DependsOn is optional, used for auto PropertyChanged firing
    [Computed(typeof(BooksModule), "GetOrderSummary"), DependsOn("User,Total,CreatedOn")] 
    string Summary { get; }

  }

  [Entity, ClusteredIndex("Order,Id")] 
  public interface IBookOrderLine {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [NoUpdate, CascadeDelete] //Delete all lines when order is deleted
    [PropagateUpdatedOn] //update Order.UpdatedOn whenever order line is updated/deleted
    IBookOrder Order { get; set; }
    int LineNumber { get; set; } //automatically maintained line order - see IBookOrder.Lines property
    int Quantity { get; set; }
    IBook Book { get; set; }
    Decimal Price { get; set; } //captures price at the time the order was created
  }

  //We track history for book review
  [Entity, OrderBy("CreatedOn"), ClusteredIndex("Book,CreatedOn,Id")] //, Modules.DataHistory.KeepHistory]
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
    [PrimaryKey] // does not create PK on view in DB, but allows treat view as entity: GetEntity<>(pk)
    Guid Id { get; set; }
    string Title { get; set; }
    string Publisher { get; set; }
    // On purpose: both columns come from aggregate and might be null in View results;
    // We make Count not nullable, Total nullable - to check proper handling 
    int Count { get; set; }
    Decimal? Total { get; set; }
  }

  // For materialized view
  public interface IBookSalesMat {
    [UniqueClusteredIndex] //SQL Server - required to define other indexes
    Guid Id { get; set; }
    [Index]
    string Title { get; set; }
    string Publisher { get; set; }
    int Count { get; set; }
    Decimal? Total { get; set; }
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

}
