using System;
using System.Collections.Generic;
using System.ComponentModel;

using Vita.Entities;
using Vita.Modules;

namespace BookStore {

  //Entities for Books module

  [Flags]
  public enum BookEdition {
    Paperback = 1,
    Hardcover = 1 << 1,
    EBook = 1 << 2,
  }

  public enum BookCategory {
    Programming,
    Fiction,
    Kids,
  }

  public enum OrderStatus {
    Open, //in progress, being edited by the user
    Completed, 
    Fulfilled,
    Canceled,
  }

  [Flags]
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
