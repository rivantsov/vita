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
    public async Task TestTopQueries() {
      TestEnv.LogTestMethodStart();
      var client = TestEnv.Client;
      TestEnv.LogTestDescr("Search query, Search books");
      var bkSearch = new BookSearchInput() {
        Title = "c", AuthorLastName = "Sharp", Categories = new [] {BookCategory.Programming},
        MaxPrice = 100.0d, Publisher = "MS Books", PublishedAfter = new DateTime(2000, 1, 1) 
      };
      var paging = new Paging() { OrderBy = "publishedOn-desc", Skip = 0, Take = 5 };
      var vars = new TVars() {
        ["search"] = bkSearch, ["paging"] = paging
      };
      var query = @"
query ($search: BookSearchInput, $paging: paging) { 
    books: searchBooks(search: $search, paging: $paging) {
        id  title 
    } 
} ";
      var resp = await client.PostAsync(query, vars);
      resp.EnsureNoErrors(); 
      var books = resp.GetTopField<Book[]>("books");
      Assert.AreEqual(1, books.Length, "Expected 1 book");
      TestEnv.LogTestDescr(" Same query, 2 more times to check timing with paths warmed up");
      resp = await client.PostAsync(query, vars);
      resp = await client.PostAsync(query, vars);
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
      authors { firstName lastName}
      editions 
      editor { userName }
    } 
  }  
}";
      var resp = await client.PostAsync(query);
      var qryCount = TestEnv.GetSqlQueryCount() - oldQueryCount; 
      var pubs = resp.GetTopField<Publisher[]>("publishers");
      Assert.IsNotNull(pubs, "expected publishers");
      Assert.IsTrue(pubs.Length > 1);
      Assert.AreEqual(4, qryCount, "Expected 4 queries: publishers, books, authors, editors");

    }



  }
}
