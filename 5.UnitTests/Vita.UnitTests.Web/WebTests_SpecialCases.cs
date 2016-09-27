using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

using Vita.Entities;
using Vita.UnitTests.Common;
using Vita.Modules.Logging;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.Api;
using Vita.Modules.WebClient.Sync;
using Vita.Modules.WebClient;
using Vita.Common;
using System.Threading.Tasks;

namespace Vita.UnitTests.Web {

  public partial class WebTests  {

    // Tests special methods in controller
    [TestMethod]
    public void TestSpecialMethods() {
      var client = Startup.Client;
      Logout(); // just in case there's still session there from other tests

      //Test date handling in URL
      // Web Api converts all datetime values in URL to local datetime. VITA provides automatic fix for this. 
      var dt = new DateTime(2016, 1, 7, 18, 19, 20, DateTimeKind.Utc); //some fixed date in UTC
      var strdt = dt.ToString("u"); // 'u' pattern displays differently local and UTC time values, we use to check local/UTC dates handling
      var strdtRet = client.ExecuteGet<string>("api/special/datetostring?dt={0}", strdt);
      Assert.AreEqual(strdt, strdtRet, "Returned date string does not match.");
      // another method with [FromUrl] input box
      var strdtRet2 = client.ExecuteGet<string>("api/special/datetostring2?date={0}", strdt);
      Assert.AreEqual(strdt, strdtRet2, "(FromULR parameter) Returned date string does not match.");

      //Test values handling in URL
      var gd = new Guid("729C7EA4-F3C5-11E4-88C8-F0DEF1783701");
      var foo = client.ExecuteGet<string>("api/special/foo/{0}/{1}", 1, gd); //foo/(int)/(guid)
      Assert.AreEqual("Foo:1," + gd, foo);
      // 'bars' method requires login
      var exc = TestUtil.ExpectFailWith<ClientFaultException>(() => client.ExecuteGet<string>("api/special/bars?q=Q"));
      Assert.AreEqual("Authentication required.", exc.Faults[0].Message); 
      LoginAs("Dora");
      var bars = client.ExecuteGet<string>("api/special/bars?q=Q");
      Assert.AreEqual("bars:Q", bars);
      Logout(); 
      //Test singleton controller
      var sfoo = client.ExecuteGet<string>("api/singleton/foo?p1={0}", "abc");
      Assert.AreEqual("Foo:abc", sfoo, "Singleton method failed.");
      //Test classic WebApi controller
      var cfoo = client.ExecuteGet<string>("api/classic/foo?p1={0}", "abc");
      Assert.AreEqual("Foo:abc", cfoo, "Classic Api controller call failed.");

      // Call getBook with bad book id - will return NotFound custom code - 
      //    it is done on purpose by controller, instead of simply returning null
      var apiExc = TestUtil.ExpectFailWith<ApiException>(() => client.ExecuteGet<Book>("api/special/books/{0}", Guid.NewGuid()));
      Assert.AreEqual(HttpStatusCode.NotFound, apiExc.Status, "Expected NotFound status");

      //Test redirect; when we create WebApiClient in SetupHelper, we set: Client.InnerHandler.AllowRedirect = false; so it will bring error on redirect 
      apiExc = TestUtil.ExpectFailWith<ApiException>(() => client.ExecuteGet<HttpStatusCode>("api/special/redirect"));
      Assert.AreEqual(HttpStatusCode.Redirect, apiExc.Status, "Expected redirect status");

    }



    [TestMethod]
    public void TestDiagnosticsController() {
      var client = Startup.Client;
      var acceptText = "application/text,text/plain";
      //Heartbeat
      DiagnosticsController.Reset();
      var serverStatus = client.ExecuteGetString(acceptText, "api/diagnostics/heartbeat");
      Assert.AreEqual("StatusOK", serverStatus, "Expected StatusOK in heartbeat"); 
      // throw error
      DiagnosticsController.Reset();
      var exc = TestUtil.ExpectFailWith<Exception>(() => client.ExecuteGetString(acceptText, "api/diagnostics/throwerror"));
      Assert.IsTrue(exc.Message.Contains("TestException"), "Expected TestException thrown");
      //get/set time offset
      // current should be zero
      var currOffset = client.ExecuteGetString(acceptText, "api/diagnostics/timeoffset");
      Assert.IsTrue(currOffset.StartsWith("Current offset: 0 minutes"), "Expected no offset");
      // set 60 minutes forward
      currOffset = client.ExecuteGetString(acceptText, "api/diagnostics/timeoffset/60");
      Assert.IsTrue(currOffset.StartsWith("Current offset: 60 minutes"), "Expected 60 minutes offset");
      // set back to 0
      currOffset = client.ExecuteGetString(acceptText, "api/diagnostics/timeoffset/0");
      Assert.IsTrue(currOffset.StartsWith("Current offset: 0 minutes"), "Expected no offset");

      //test that heartbeat call is not logged in web log - controller method sets log level to None
      var serverSession = Startup.BooksApp.OpenSession();
      var hbeatEntry = serverSession.EntitySet<IWebCallLog>().Where(wl => wl.Url.Contains("heartbeat")).FirstOrDefault();
      Assert.IsNull(hbeatEntry, "Expected no heartbeat entry in web log.");
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
      var ok = client.ExecuteGet<string>("api/special/connectiontest");
      Assert.AreEqual("OK", ok, "Connection test did not return OK");
      //get report
      var report = client.ExecuteGet<string>("api/special/connectiontestreport");
      // values: State is closed, DataConnection.Close was called, WebCallContextHandler.PostProcessResponse was called
      Assert.AreEqual("True,True,True", report, "Connection report does not match expected value");
    }

    [TestMethod]
    public void TestAsyncServerMethod() {
      var client = Startup.Client;
      var result = client.ExecuteGet<string>("api/special/getdateasync");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(result), "Async method call failed.");
    }

    [TestMethod]
    public void TestClientCallAsync() {
      AsyncHelper.RunSync(() => TestClientCallAsyncImpl());      
    }
    public async Task TestClientCallAsyncImpl() {
      var client = Startup.Client;
      //Test WebApiClient.CallAsync
      var res2 = await client.SendAsync<object, string>(System.Net.Http.HttpMethod.Get, null, "api/special/getdateasync");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(res2), "Async method call failed.");
    }

  }//class
}
