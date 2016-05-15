using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common; 
using Vita.Entities;
using Vita.Data;
using Vita.Data.Driver;
using Vita.UnitTests.Common;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.Api;

namespace Vita.UnitTests.Extended {

  [TestClass]
  public class LinqSearchTests {

    [TestInitialize]
    public void TestInit() {
      SetupHelper.InitApp();
    }

    [TestCleanup]
    public void TearDown() {
      SetupHelper.TearDown();
    }
  
    [TestMethod]
    public void TestLinqSearch() {
      var app = SetupHelper.BooksApp;
      var session = app.OpenSession();
      //We use catalog controller's SearchBooks method to invoke search method. 
      // This is also a demo of using Api controllers outside Web server environment
      var contr = new CatalogController(app.CreateSystemContext());

      //User search helper method with all possible search terms. Let's find c# book
      var searchParams = new BookSearch() {
        Title = "C#", Categories = "Programming,Fiction", MaxPrice = 100.0, Publisher = "MS",
          PublishedAfter = new DateTime(2000, 1, 1), PublishedBefore = DateTime.Now, 
          AuthorLastName = "Sharp", OrderBy = "Price-desc,pubname,PublishedOn-desc",
          Skip = 0, Take = 5
      };
      var bookResults = contr.SearchBooks(searchParams);
      //PrintLastSql(session);
      Assert.AreEqual(1, bookResults.Results.Count, "Failed to find c# book");
      /* Here's the resulting SQL for MS SQL 2012:
      
SELECT f$.[Id], f$.[Title], f$.[Description], f$.[PublishedOn], f$.[Abstract], f$.[Category], f$.[Editions], f$.[Price], 
        f$.[CreatedIn], f$.[UpdatedIn], f$.[Publisher_Id]
  FROM [books].[Book] f$, [books].[Publisher] t1$
  WHERE (t1$.[Id] = f$.[Publisher_Id]) AND ((f$.[Title] LIKE @P5 ESCAPE '\') AND 
        (f$.[Price] <= @P0) AND (t1$.[Name] LIKE @P6 ESCAPE '\') AND 
        (f$.[PublishedOn] >= @P1) AND 
        (f$.[PublishedOn] <= @P2) AND
        f$.[Category] IN (0, 1) AND 
        (f$.[Id] IN (SELECT ba$.[Book_Id]
                     FROM [books].[BookAuthor] ba$, [books].[Author] t2$
                     WHERE (t2$.[Id] = ba$.[Author_Id]) AND (t2$.[LastName] LIKE @P7 ESCAPE '\'))))
  ORDER BY f$.[Price] DESC, t1$.[Name], f$.[PublishedOn] DESC 
  OFFSET @P3 ROWS FETCH NEXT @P4 ROWS ONLY 
-- Parameters: @P0=100, @P1=[2000-01-01T00:00:00], @P2=[2015-02-28T02:05:42], @P3=0, @P4=5, @P5='c#%', @P6='MS%', @P7='Sharp%' 
       */

      // run with empty terms, 'get-any-top-10' 
      searchParams = new BookSearch() {Take = 10};
      bookResults = contr.SearchBooks(searchParams);
      Assert.IsTrue(bookResults.Results.Count > 3, "Must return all books");

    }

    private void PrintLastSql(IEntitySession session) {
      //Printout SQL query that was executed
      var cmd = session.GetLastCommand();
      Debug.WriteLine(cmd.ToLogString());
    }

  }
}
