using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Samples.BookStore;
using Vita.Data.Driver;
using Vita.Modules.Login;
using Vita.Tools.Testing;
using Vita.Data.Sql;

namespace Vita.Testing.ExtendedTests {

  public partial class LinqTests {

    //Helper class to use as output in queries - testing LINQ engine with custom (non-anon) output types
    [DebuggerDisplay("{Title},{Publisher},{Price}")]
    class BookInfo {
      public string Title;
      public string Publisher;
      public decimal Price;

      public BookInfo() { }
      public BookInfo(string title, string publisher, decimal price) {
        Title = title;
        Publisher = publisher;
        Price = price;
      }
    }

    class EditorObj {
      public Guid EditorId;
      public UserType UserType;
      public string UserName;
    }

    [TestMethod]
    public void TestLinqReturnCustomObject() {
      var session = Startup.BooksApp.OpenSession();
      var books = session.EntitySet<IBook>();

      // query with custom type in output (not anon type)
      var qBkInfos = from b in books
                     where b.Price > 1
                     select new BookInfo() { Price = b.Price, Title = b.Title, Publisher = b.Publisher.Name };
      var lstBkInfos = qBkInfos.ToList();
      Assert.IsTrue(lstBkInfos.Count > 0, "BookInfo query failed.");

      // Same with non-default constructor
      var qBkInfos2 = from b in books
                      where b.Price > 1
                      select new BookInfo(b.Title, b.Publisher.Name, b.Price) { Title = b.Title };
      var lstBkInfos2 = qBkInfos2.ToList();
      Assert.IsTrue(lstBkInfos2.Count > 0, "BookInfo query failed.");

      // bug fix - Linq with out object filled from GroupBy over nullable key
      // book.Editor is nullable; b.Editor.Id is translated into Guid? expression. 
      // Linq engine adds a conversion that return default(Guid) if coming value is null. 
      // We also test enum and string values
      var bkCounts = books
        .Select(b => new EditorObj() {
          EditorId = b.Editor.Id, UserName = b.Editor.UserName, UserType = b.Editor.Type
        }
        ).ToList();
      Assert.IsTrue(bkCounts.Count > 0, "Expected some objects");

    }


    [TestMethod]
    public void TestLinqDates() {
      // SQLite date/time functions return strings, so tests do not work
      if (Startup.ServerType == DbServerType.SQLite)
        return;

      var session = Startup.BooksApp.OpenSession();
      var books = session.EntitySet<IBook>();
      var bk1 = books.First();

      var createdOn = bk1.CreatedOn;
      IList<IBook> bookList;
      IDbCommand cmd;

      bookList = books.Where(b => b.CreatedOn.Date == createdOn.Date).ToList();
      cmd = session.GetLastCommand();
      Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.Date");

      switch (Startup.ServerType) {
        //MySql, Postgres, Oracle TIME() not supported
        case DbServerType.MySql:
        case DbServerType.Postgres:
        case DbServerType.Oracle:
          break;
        default:
          bookList = books.Where(b => b.CreatedOn.TimeOfDay == createdOn.TimeOfDay).ToList();
          cmd = session.GetLastCommand();
          Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.TimeOfDay");
          break;
      }

      bookList = books.Where(b => b.CreatedOn.Year == createdOn.Year).ToList();
      cmd = session.GetLastCommand();
      Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.Year");

      bookList = books.Where(b => b.CreatedOn.Day == createdOn.Day).ToList();
      cmd = session.GetLastCommand();
      Assert.IsTrue(bookList.Count > 0, "Expected some book by CreatedOn.Day");
    }

    [TestMethod]
    public void TestLinqBoolBitColumns() {
      // We test that LINQ engine correctly handles bit fields
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var users = session.EntitySet<IUser>();

      bool boolParam = true;
      //Bug fix, handling expressions like 'ent.BoolProp & boolValue == ent.BoolProp'
      var q0 = from u in users
               where (u.IsActive && boolParam) == u.IsActive
               select u;
      var lstUsers0 = q0.ToList();

      // MS SQL, MySql : bit field is integer, so
      // expression over bit field: 'u.IsActive==true' should be replaced with 'u.IsActive = 1'; we also check ! operator and bool parameter
      // Postgress has boolean data type, so it should be used as is.
      var q2 = from u in users
               where u.IsActive && u.IsActive == true && u.IsActive == boolParam && true || boolParam || !u.IsActive
               select u;
      var lstUsers2 = q2.ToList();
      LogLastQuery(session);
      Assert.IsTrue(lstUsers2.Count > 0, "Bit field expr test failed");

      // bool/bit field used in Where expressions directly - should be replaced with 'u.IsActive = 1'
      var q1 = from u in users
               where u.IsActive
               select u;
      var lstUsers1 = q1.ToList();
      Assert.IsTrue(lstUsers1.Count > 0, "No active users found.");

      // Using bit field in anon type initializer
      var q3 = from u in users
                 //where u.IsActive
               select new { U = u.UserName, A = u.IsActive };
      var lstUsers3 = q3.ToList();
      Assert.IsTrue(lstUsers3.Count > 0, "Bit field - use in anon object failed.");

      Startup.BooksApp.Flush();
    }


    [TestMethod]
    public void TestLinqBoolOutputColumns() {
      // We test that LINQ engine correctly handles bool values in return columns
      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // Some servers (MS SQL, Oracle) do not support bool as valid output type
      //   So SQL like "SELECT (2 > 1) As BoolValue" fails
      // LINQ engine automatically adds IIF(<boolValue>, 1, 0)
      // Another trouble: MySql stores bools as UInt64, but comparison results in Int64
      // Oracle does not allow queries without FROM, so engine automatically adds 'FROM dual' which if fake FROM clause
      session = app.OpenSession();
      var hasFiction = session.EntitySet<IBook>().Any(b => b.Category == BookCategory.Fiction);
      Assert.IsTrue(hasFiction, "Expected hasFiction to be true");

      // another variation
      var books = session.EntitySet<IBook>().Where(b => b.Authors.All(a => a.LastName != null)).ToArray();
      Assert.IsTrue(books.Length > 0, "Expected all books");

    }

    [TestMethod]
    public void TestLinqWithNullables() {
      var session = Startup.BooksApp.OpenSession();

      //First query using literal null; this alwasy worked OK, SQL generated is "WHERE b.Abstract IS NULL"
      var qNotPublished = from b in session.EntitySet<IBook>()
                          where b.PublishedOn == null
                          select b;
      var booksNotPublished = qNotPublished.ToList();
      var countNotPublished = booksNotPublished.Count;
      Assert.IsTrue(countNotPublished > 0, "Expected non-published book ");

      // Bug fix. Using a variable instead of literal null.
      //   Before fix: the query was using 'WHERE b.PublishedOn = @P1", which fails to match null values
      //   After fix: the SQL is 'WHERE (b.Abstract == @P1 OR (b.Abstract IS NULL) AND (@P1 IS NULL))'
      DateTime? nullDate = null;
      var qNotPublished2 = from b in session.EntitySet<IBook>()
                           where b.PublishedOn == nullDate
                           select b;
      var booksNotPublished2 = qNotPublished2.ToList();
      var countNotPublished2 = booksNotPublished2.Count;
      Assert.AreEqual(countNotPublished, countNotPublished2, "Null query failed, expected the same book count.");


      // Nullable entity refs and strings
      IUser someEditor = null;
      string someCode = null;
      var qNullCompare = from b in session.EntitySet<IBook>()
                         where b.SpecialCode == someCode || b.Editor == someEditor
                         select b;
      var booksNullCompare = qNullCompare.ToList();
      var cmd = session.GetLastCommand();
      Assert.IsTrue(booksNullCompare.Count > 0, "Expected some books without editor");
    }


  }//class

}
