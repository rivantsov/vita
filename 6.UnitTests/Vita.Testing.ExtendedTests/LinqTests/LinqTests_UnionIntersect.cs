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

    [TestMethod]
    public void TestLinqUnion() {
      var app = Startup.BooksApp;
      IDbCommand lastCmd;

      var session = app.OpenSession();
      var books = session.EntitySet<IBook>();

      // Union query
      var fictionAndProgBooks = books.Where(b => b.Category == BookCategory.Fiction)
                                .Union(books.Where(b => b.Category == BookCategory.Programming))
                                .ToList();
      lastCmd = session.GetLastCommand();
      // get books with regular no-union query
      var fictionAndProgBooksVerify = books.Where(b => b.Category == BookCategory.Fiction || b.Category == BookCategory.Programming)
                                .ToList();
      Assert.AreEqual(fictionAndProgBooksVerify.Count, fictionAndProgBooks.Count, "Invalid book count");

    }

  } //class
}
