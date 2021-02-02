using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BookStore;
using BookStore.GraphQLServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NGraphQL.Client;


namespace Vita.Testing.GraphQLTests {
  using TVars = Dictionary<string, object>;

  [TestClass]
  public class BasicTests {

    [TestInitialize]
    public void TestInit() {
      TestEnv.Init(); 
    }

    [TestCleanup]
    public void TestCleanup() {
      TestEnv.FlushLogs(); 
    }

    [TestMethod]
    public async Task TestBasicQueries() {
      TestEnv.LogTestMethodStart(); 
      var client = TestEnv.Client;
      var query = @"query { publishers { id  name } } ";
      var resp = await client.PostAsync(query);
      var pubs = resp.GetTopField<Publisher[]>("publishers");
      Assert.IsNotNull(pubs, "expected publishers");
      Assert.IsTrue(pubs.Length > 1);

      TestEnv.LogTestDescr("Repeating the same query 2 more times, to check request timing after the execution path is warmed up.");
      TestEnv.LogText("     (use Run command, not Debug in Test Explorer)");
      resp = await client.PostAsync(query);
      resp = await client.PostAsync(query);


    }

    [TestMethod]
    public async Task TestBooksSearch() {
      TestEnv.LogTestMethodStart();
      var client = TestEnv.Client;
      TestEnv.LogTestDescr("Search query, Search books");
      var bkSearch = new BookSearchInput() {
        Title = "c", AuthorLastName = "Sharp", Editions = BookEdition.Hardcover | BookEdition.Paperback, 
        Categories = new [] {BookCategory.Programming},
        MaxPrice = 100.0d, Publisher = "MS Books", PublishedAfter = new DateTime(2000, 1, 1) 
      };
      var paging = new Paging() { OrderBy = "publishedOn-desc", Skip = 0, Take = 5 };
      var vars = new TVars() {
        ["search"] = bkSearch, ["paging"] = paging
      };
      var query = @"
query ($search: BookSearchInput, $paging: paging) { 
    books: searchBooks(search: $search, paging: $paging) {
        id  
        title
        publishedOn
        publisher {name}
        authors { fullName}
        editions 
        editor { userName }
    } 
} ";
      var oldQueryCount = TestEnv.GetSqlQueryCount();
      var resp = await client.PostAsync(query, vars);
      resp.EnsureNoErrors(); 
      var books = resp.GetTopField<Book[]>("books");
      Assert.AreEqual(1, books.Length, "Expected 1 book");
      var qryCount = TestEnv.GetSqlQueryCount() - oldQueryCount;
      // queries: books (main search), publisher, authors, users (for editor)
      Assert.AreEqual(4, qryCount, "Expected 4 DB queries.");
    }

    [TestMethod]
    public async Task TestSmartLoad() {
      TestEnv.LogTestMethodStart();
      TestEnv.LogTestDescr(" VITA's smart load feature, child lists are loaded automatically for ALL parent records in one call.");
      var client = TestEnv.Client;
      var oldQueryCount = TestEnv.GetSqlQueryCount(); 
      var query = @"
query myquery { 
  publishers {
    id 
    name
    books {
      id 
      title 
      authors { fullName}
      editions 
      editor { userName }
      reviews {
        createdOn
        user {userName}
        rating
        caption
      }
    } 
  }  
}";
      var resp = await client.PostAsync(query);
      resp.EnsureNoErrors();
      var qryCount = TestEnv.GetSqlQueryCount() - oldQueryCount; 
      var pubs = resp.GetTopField<Publisher[]>("publishers");
      Assert.IsNotNull(pubs, "expected publishers");
      Assert.IsTrue(pubs.Length > 1);
      // batched queries: 
      // 6 queries: publishers, books, authors, editors (users), reviews, reviewers (users) 
      Assert.AreEqual(6, qryCount, "Expected 6 queries");
    }



  }
}
