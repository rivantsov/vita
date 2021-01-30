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
      var client = TestEnv.Client;
      var query = @"query { publishers {  name } } ";
      var resp = await client.PostAsync(query);
      var pubs = resp.GetTopField<Publisher[]>("publishers");
      Assert.IsNotNull(pubs, "expected publishers");
      Assert.IsTrue(pubs.Length > 1);
    }
  }
}
