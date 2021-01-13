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
    public void TestLinqUnionIntersect() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      IDbCommand lastCmd;

      var session = app.OpenSession();
      // We could use books entitySet as-is, but Oracle does not like blob columns in UNION (book.Abstract is text blob);
      // so we use this sub-selection of columns
      var books = session.EntitySet<IBook>()
        .Select(b => new { b.Id, b.Title, b.Category, b.Price });

      // Union query
      var unionList = books.Where(b => b.Category == BookCategory.Fiction)
                                .Union(books.Where(b => b.Category == BookCategory.Programming))
                                .ToList();
      lastCmd = session.GetLastCommand();
      LogLastQuery(session);
      Assert.IsTrue(unionList.Count > 0, "Empty list in UNION");

      var fictionAndProgBooks2 = books.Where(b => b.Category == BookCategory.Fiction)
                                .Concat(books.Where(b => b.Category == BookCategory.Programming))
                                .ToList();
      LogLastQuery(session);

      if (Startup.Driver.Supports(DbFeatures.ExceptOperator)) {
        var exceptList = books.Where(b => b.Category == BookCategory.Fiction || b.Category == BookCategory.Programming)
                             .Except(books.Where(b => b.Category == BookCategory.Programming))
                              .ToList();
        LogLastQuery(session);
        Assert.IsTrue(exceptList.Count > 0, "Empty list in EXCEPT");
      }

      if (Startup.Driver.Supports(DbFeatures.IntersectOperator)) {
        var intersectList = books.Where(b => b.Category == BookCategory.Fiction || b.Category == BookCategory.Programming)
                             .Intersect(books.Where(b => b.Category == BookCategory.Programming))
                              .ToList();
        LogLastQuery(session);
        Assert.IsTrue(intersectList.Count > 0, "Empty list in INTERSECT");
      }
    }

  } //class
}
