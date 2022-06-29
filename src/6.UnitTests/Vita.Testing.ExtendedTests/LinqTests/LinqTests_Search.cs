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
using Vita.Tools.Testing;
using BookStore;

namespace Vita.Testing.ExtendedTests {

  public partial class LinqTests {

    [TestMethod]
    public void TestLinqSearch() {
      Startup.BooksApp.LogTestStart();

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
      searchParams = new BookSearch() { Take = 10 };
      bookResults = session.SearchBooks(searchParams);
      Assert.IsTrue(bookResults.Results.Count > 3, "Must return all books");

      // bug, issue #74 - when Skip is large value > Count(entities), then search returns total count == skip value 
      var totalCount = session.EntitySet<IBook>().Count();
      searchParams = new BookSearch() { Skip = 200, Take = 10 };
      bookResults = session.SearchBooks(searchParams);
      Assert.AreEqual(totalCount, bookResults.TotalCount, "Total count mismatch for larget Skip value");
    }


    [TestMethod]
    public void TestLinq_Search_ListProps() {
      Startup.BooksApp.LogTestStart();

      // SQLite sometimes fails this test (fails to find a book), but only when running ALL tests in extended project, 
      // for no apparent reason; trying to fix it
      if(Startup.ServerType == DbServerType.SQLite) {
        System.Threading.Thread.Sleep(100);
        System.Threading.Thread.Sleep(100);
      }

      var app = Startup.BooksApp;
      var session = app.OpenSession();

      session.LogMessage("----------- testing LINQ with sub-query on list props, one to many  -------------------------");
      var csPubs = session.EntitySet<IPublisher>()
          .Where(p => p.Books.Any(b => b.Title == "c# Programming"))
          .ToList();
      Assert.AreEqual(1, csPubs.Count, "expected 1 pub for c# book");

      session.LogMessage("----------- testing LINQ with sub-query on list props, many to many  -------------------------");
      var booksByJack = session.EntitySet<IBook>()
          .Where(b => b.Authors.Any(a => a.FirstName == "Jack"))
          .ToList();
      Assert.AreEqual(2, booksByJack.Count, "expected 2 books by Jack");

    }


    [TestMethod]
    public void TestLinqSearchWithOR() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.LogMessage("----------- Using OR in search expressions test -------------------------");

      // Search for books published after dateX, with title starting with any of 2 terms
      var pubAfter = new DateTime(2000, 1, 1);
      string term1 = "c#";
      string term2 = "VB";

      var where = session.NewPredicate<IBook>()
        .AndIfNotEmpty(pubAfter, b => b.PublishedOn.Value >= pubAfter);
      // Build optional OR clause
      var emptyClause = session.NewPredicate<IBook>(false); // important! should be False since its OR expr
      var termMatchClause = emptyClause
        .OrIfNotNull(term1, b => b.Title.StartsWith(term1))
        .OrIfNotNull(term2, b => b.Title.StartsWith(term2));
      // check if we added any conditions to OR expr
      if(termMatchClause != emptyClause)
        where = where.And(termMatchClause);

      // Execute
      var searchPrms = new SearchParams() { OrderBy = "Title", Take = 10 };
      var res = session.ExecuteSearch(where, searchPrms);
      var cmd = session.GetLastCommand();
      Assert.AreEqual(2, res.Results.Count, "expected 2 books");
      
/* SQL: 

SELECT  "Id", "Title", "Description", "PublishedOn", "Abstract", "Category", "Editions", "Price", "WholeSalePrice", "SpecialCode", "Isbn", "CreatedOn", "Publisher_Id", "CoverImage_Id", "Editor_Id"
FROM "books"."Book"
WHERE 1 = 1 AND "PublishedOn" >= @P0 AND (1 <> 1 OR "Title" LIKE @P1 ESCAPE '\' OR "Title" LIKE @P2 ESCAPE '\')
ORDER BY "Title"
OFFSET @P4 ROWS FETCH NEXT @P3 ROWS ONLY;

 */


    }
  }
}
