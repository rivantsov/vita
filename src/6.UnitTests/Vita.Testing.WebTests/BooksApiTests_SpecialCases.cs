using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

using Vita.Entities;
using BookStore;
using BookStore.Api;
using System.Threading.Tasks;
using Vita.Tools.Testing;
using Arrest;
using Arrest.Sync;

namespace Vita.Testing.WebTests {

  public partial class BooksApiTests  {

    // Tests special methods in controller
    [TestMethod]
    public void TestSpecialMethods() {
      var client = Startup.Client;
      Logout(); // just in case there's still session there from other tests

      //Test values handling in URL
      var gd = new Guid("729C7EA4-F3C5-11E4-88C8-F0DEF1783701");
      var foo = client.Get<string>("api/special/foo/{0}/{1}", 1, gd); //foo/(int)/(guid)
      Assert.AreEqual("Foo:1," + gd, foo);
      // 'bars' method requires login
      var exc = TestUtil.ExpectFailWith<RestException>(() => client.Get<string>("api/special/bars?q=Q"));
      Assert.AreEqual(HttpStatusCode.Unauthorized, exc.Status, "Expected Unauthorized"); 
      LoginAs("Dora");
      var bars = client.Get<string>("api/special/bars?q=Q");
      Assert.AreEqual("bars:Q", bars);
      Logout(); 

      // Call getBook with bad book id - will return NotFound custom code - 
      //    it is done on purpose by controller, instead of simply returning null
      var apiExc = TestUtil.ExpectFailWith<RestException>(() => client.Get<Book>("api/special/books/{0}", Guid.NewGuid()));
      Assert.AreEqual(HttpStatusCode.NotFound, apiExc.Status, "Expected NotFound status");

      //Test redirect; when we create WebApiClient in SetupHelper, we set: Client.InnerHandler.AllowRedirect = false; so it will bring error on redirect 
      apiExc = TestUtil.ExpectFailWith<RestException>(() => client.Get<HttpStatusCode>("api/special/redirect"));
      Assert.AreEqual(HttpStatusCode.Redirect, apiExc.Status, "Expected redirect status");

    }


    [TestMethod]
    public void TestDiagnosticsController() {
      var client = Startup.Client;
      var acceptText = "application/text,text/plain";
      //Heartbeat
      DiagnosticsController.Reset();
      var serverStatus = client.GetString("api/diagnostics/heartbeat", null, acceptText);
      Assert.AreEqual("StatusOK", serverStatus, "Expected StatusOK in heartbeat"); 
      // throw error
      DiagnosticsController.Reset();
      var exc = TestUtil.ExpectFailWith<Exception>(() => client.GetString("api/diagnostics/throwerror", null, acceptText));
      Assert.IsTrue(exc.Message.Contains("TestException"), "Expected TestException thrown");
      //get/set time offset
      // current should be zero
      var currOffset = client.GetString("api/diagnostics/timeoffset", null, acceptText);
      Assert.IsTrue(currOffset.StartsWith("Current offset: 0 minutes"), "Expected no offset");
      // set 60 minutes forward
      currOffset = client.GetString("api/diagnostics/timeoffset/60", null, acceptText);
      Assert.IsTrue(currOffset.StartsWith("Current offset: 60 minutes"), "Expected 60 minutes offset");
      // set back to 0
      currOffset = client.GetString("api/diagnostics/timeoffset/0", null, acceptText);
      Assert.IsTrue(currOffset.StartsWith("Current offset: 0 minutes"), "Expected no offset");

      /*
      //test that heartbeat call is not logged in web log - controller method sets log level to None
      var serverSession = TestStartup.BooksApp.OpenSession();

      // fix this
      var hbeatEntry = serverSession.EntitySet<IWebCallLog>().Where(wl => wl.Url.Contains("heartbeat")).FirstOrDefault();
      Assert.IsNull(hbeatEntry, "Expected no heartbeat entry in web log.");
      */
    }

    // Tests KeepOpen connection mode (default for web controllers). In this mode, the connection to database is kept alive in entity session between db calls, 
    // to avoid extra work on acquiring connection from pool, and on making 'reset connection' roundtrip to db (done by connection pool when it gives connection from the pool)
    // Web call is usually very quick, so it's reasonable to hold on to open connection until the end. If the connection is not closed explicitly 
    // by controller method, it is closed automatically by Web call handler (conn is registered in OperationContext.Disposables).
    // The following test verifies this behavior. We make a test call which executes a simple query to database, 
    // then checks that connection is still alive and open (saved in session.CurrentConnection).
    // Then it sets up an event handler that would register the conn.Close call (conn.StateChanged event) which will happen when Web call completes; 
    //  the handler saves a 'report' in a static field in controller. 
    // We then retrieve this report thru second call and verify that connection was in fact closed properly. 
    [TestMethod]
    public void TestDbConnectionHandling() {
      var client = Startup.Client;
      var ok = client.Get<string>("api/special/connectiontest");
      Assert.AreEqual("OK", ok, "Connection test did not return OK");
      //get report
      var report = client.Get<string>("api/special/connectiontestreport");
      // values: State is closed, DataConnection.Close was called, WebCallContextHandler.PostProcessResponse was called
      Assert.AreEqual("True,True,True", report, "Connection report does not match expected value");
    }


  }//class
}
