using System;
using System.Collections.Generic;
using System.ComponentModel;

using Vita.Entities;
using Vita.Modules;

namespace Vita.Samples.BookStore {

  //Entities for Books module

  [Flags, Description("Available editions.")]
  public enum BookEdition {
    [Description("Paperback edition.")]
    Paperback = 1,
    [Description("Hardcover edition.")]
    Hardcover = 1 << 1,
    [Description("Electronic download.")]
    EBook = 1 << 2,
  }

  [Description("Book category.")]
  public enum BookCategory {
    [Description("Books on programming.")]
    Programming,
    [Description("Fiction.")]
    Fiction,
    [Description("Kids books.")]
    Kids,
  }

  public enum OrderStatus {
    Open, //in progress, being edited by the user
    Completed, 
    Fulfilled,
    Canceled,
  }

  [Flags, Description("User types.")]
  public enum UserType {
    Customer = 1,
    Author = 1 << 1,
    BookEditor = 1 << 2,
    CustomerSupport = 1 << 3,
    StoreAdmin = 1 << 4,
  }

  public enum ImageType {
    Unknown,
    BookCover, 
    AuthorPic,
  }
}
