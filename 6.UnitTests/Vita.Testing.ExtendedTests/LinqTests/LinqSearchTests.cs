using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.Api;

namespace Vita.Testing.ExtendedTests {

  [TestClass]
  public class LinqSearchTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }

    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }
  
    [TestMethod]
    public void TestLinqSearch() {
      // SQLite sometimes fails this test (fails to find a book), but only when running ALL tests in extended project, 
      // for no apparent reason; trying to fix it
      if(Startup.ServerType == DbServerType.SQLite) {
        System.Threading.Thread.Sleep(100);
        System.Threading.Thread.Sleep(100);
      }

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.LogMessage("----------- Books Search Tests -------------------------");

      //Use search helper method with all possible search terms. Let's find c# book
      var searchParams = new BookSearch() {
        Title = "c#", Categories = "Programming,Fiction", MaxPrice = 100.0, Publisher = "MS",
          PublishedAfter = new DateTime(2000, 1, 1), PublishedBefore = DateTime.Now, 
          AuthorLastName = "Sharp", OrderBy = "Price-desc,pubname,PublishedOn-desc",
          Skip = 0, Take = 5
      };
      var bookResults = session.SearchBooks(searchParams);
      //PrintLastSql(session);
      Assert.AreEqual(1, bookResults.Results.Count, "Failed to find c# book");
      /* Here's the resulting SQL for MS SQL Server:
      
SELECT  f$."Id", f$."CreatedOn", f$."Title", f$."Description", f$."PublishedOn", f$."Abstract", f$."Category", 
        f$."Editions", f$."Price", f$."Publisher_Id", f$."CoverImage_Id", f$."Editor_Id"
FROM "books"."Book" f$
     INNER JOIN "books"."Publisher" t0$ ON t0$."Id" = f$."Publisher_Id"
WHERE 1 = 1 AND f$."Title" LIKE @P6 ESCAPE '\' AND f$."Price" <= @P0 AND t0$."Name" LIKE @P7 ESCAPE '\' 
      AND f$."PublishedOn" >= @P1 AND f$."PublishedOn" <= @P2 
      AND (f$."Category" IN (SELECT CAST("Value" AS Int ) FROM @P3)) 
      AND f$."Id" IN ((SELECT  ba$."Book_Id"
                          FROM "books"."BookAuthor" ba$
                                INNER JOIN "books"."Author" t1$ ON t1$."Id" = ba$."Author_Id"
                          WHERE t1$."LastName" LIKE @P8 ESCAPE '\'))
ORDER BY f$."Price" DESC, t0$."Name", f$."PublishedOn" DESC
OFFSET @P4 ROWS FETCH NEXT @P5 ROWS ONLY;
 
-- Parameters: @P0=100, @P1=[2000-01-01T00:00:00], @P2=[2018-08-28T09:31:28], 
    @P3=[[ ..... ]], @P4=0, @P5=6, @P6='C#%', @P7='MS%', @P8='Sharp%' 
       */

      // run with empty terms, 'get-any-top-10' 
      searchParams = new BookSearch() {Take = 10};
      bookResults = session.SearchBooks(searchParams);
      Assert.IsTrue(bookResults.Results.Count > 3, "Must return all books");

      // bug, issue #74 - when Skip is large value > Count(entities), then search returns total count == skip value 
      var totalCount = session.EntitySet<IBook>().Count(); 
      searchParams = new BookSearch() { Skip = 200, Take = 10 };
      bookResults = session.SearchBooks(searchParams);
      Assert.AreEqual(totalCount, bookResults.TotalCount, "Total count mismatch for larget Skip value");


    }

    private void PrintLastSql(IEntitySession session) {
      //Printout SQL query that was executed
      var cmd = session.GetLastCommand();
      Debug.WriteLine(cmd.ToLogString());
    }

  }
}
