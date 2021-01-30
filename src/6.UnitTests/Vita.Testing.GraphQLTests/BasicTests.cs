using System; 
using System.Diagnostics;
using System.Threading.Tasks;
using BookStore.GraphQLServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NGraphQL.Client;

namespace Vita.Testing.GraphQLTests {
  
  [TestClass]
  public class BasicTests {

    [TestInitialize]
    public void TestInit() {
      TestEnv.Init(); 
    }

    [TestCleanup]
    public void TestCleanup() {
      TestEnv.ShutDown();
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
