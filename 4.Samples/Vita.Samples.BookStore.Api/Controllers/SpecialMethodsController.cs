using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Vita.Entities;
using Vita.Entities.Api;
using System.Data.SqlClient;
using System.Data;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {

  // Service controller with special methods for testing error handling
  [Route("api/special")]
  public class SpecialMethodsController : BaseApiController {

    // Throws null ref exception
    [HttpGet("nullref")]
    public string ThrowNullRef() {
      //Before we throw, let's write smth to local log; 
      OpContext.WriteLogMessage("Throwing NullReference exception...");
      string foo = null;
      if(foo.Length > 10)
        foo = "bar";
      return foo; 
    }

    // Illustrates/tests returning custom HTTP status code.
    // In real apps, NotFound should be left to situations when endpoint is not found (bad URL, page or api method does not exist).
    [HttpGet("books/{id}")]
    public Book GetBook(Guid id) {
      var session = OpContext.OpenSystemSession();
      var bk = session.GetEntity<IBook>(id);
      if(bk == null) {
        OpContext.WebContext.SetResponse(status : HttpStatusCode.NotFound);
        return null;
      }
      return bk.ToModel(details: true);
    }


    // this method is used for testing handling error of bad parameter types
    [HttpGet("foo/{p1}/{p2}")]
    public string Foo(int p1, Guid p2) {
      // read parameters (from part of URL and query)
      return "Foo:" + p1 + "," + p2;
    }

    public class BarParams {
      public string P1 { get; set; }
      public int P2 { get; set; }
    }

    //Test complex object with FromUrl attribute - typical use is Search queries
    // We also test that OperationContext parameter is injected automatically
    [HttpGet("bar")]
    public string Bar([FromQuery] BarParams prms, OperationContext context) {
      Util.Check(context != null, "OperationContext not inserted.");
      return prms.P1 + prms.P2;
    }

    // method requires login
    [HttpGet("bars"), Authorize]
    public string Bars(string q) {
      return "bars:" + q;
    }

    //See comments in TestDbConnectionHandling for details
    static string _connectionCloseReport;
    [HttpGet("connectiontest")]
    public string TestConnectionHandling() {
      // connection mode should be set in WebCallContextHandlerSettings, and it is reuse by default
      if(OpContext.DbConnectionMode != DbConnectionReuseMode.KeepOpen)
        return "Error: Connection mode is not KeepOpen. Mode: " + OpContext.DbConnectionMode;
      _connectionCloseReport = "(empty)"; 
      var session = OpContext.OpenSession();
      session.EnableCache(false);
      var bk = session.EntitySet<IBook>().OrderBy(b => b.Title).First(); 
      //this should creaet connection and attach to session
      var entSession = (Vita.Entities.Runtime.EntitySession) session;
      var currConn = entSession.CurrentConnection;
      if(currConn == null)
        return "Connection was not attached to entity session.";
      var sqlConn = (SqlConnection)currConn.DbConnection;
      if(sqlConn.State != ConnectionState.Open)
        return "Connection was not kept open.";
      sqlConn.StateChange += Conn_StateChange;
      return "OK";
    }

    void Conn_StateChange(object sender, StateChangeEventArgs e) {
      bool isClosed = e.CurrentState == ConnectionState.Closed;
      //Check that it is being closed by proper calls, not by garbage collector cleaning up
      var callStack = Environment.StackTrace;
      bool calledFromVitaConnClose = callStack.Contains("Vita.Data.Runtime.DataConnection.Close()");
      bool calledFromVitaMiddleware = callStack.Contains("Vita.Web.VitaWebMiddleware.EndRequest");
      _connectionCloseReport = string.Format("{0},{1},{2}", isClosed, calledFromVitaConnClose, calledFromVitaMiddleware);
    }

    [HttpGet("connectiontestreport")]
    public string GetConnectionnTestReport() {
      return _connectionCloseReport;
    }

    //Testing the bug fix - calling method with redirect was failing in attempt to deserialize response
    [HttpGet("redirect")]
    public void RedirectToSearch() {
      var webCtx = OpContext.WebContext;
      webCtx.SetResponse(status: HttpStatusCode.Redirect);
      webCtx.SetResponseHeader("Location", "http://www.google.com");
    }

    #region test methods - sync/async bridge; manually in browser only
    // these methods test/validate sync/async bridge utitilies (trouble with deadlocks)
    // it is important that these methods are run from inside IIS host (full ASP.NET host)
    // so we call these manually when running SampleBooksUI sample web app - by typing 
    // URL in browser
    /// <summary>Internal testing only. Test method to play with thread deadlocks.</summary>
    /// <returns>Does not return, hangs forever - thread deadlock.</returns>
    [HttpGet("deadlock")]
    public async Task<IActionResult> TestAsyncWithDeadlock() {
      await Task.Delay(50);
      //this should deadlock
      var result = DoDelaySyncIncorrect();
      return result;
    }

    /// <summary>Internal testing only: test sync/async bridge</summary>
    /// <returns>Current time</returns>
    [HttpGet("nodeadlock")]
    public async Task<IActionResult> TestAsyncNoDeadlock() {
      await Task.Delay(50);
      return DoDelaySyncCorrect();
    }

    private IActionResult DoDelaySyncCorrect() {
      AsyncHelper.RunSync(() => DoDelayAsync()); //this is our helper method, we test it here under IIS
      return AsPlainText("Time: " + DateTime.Now.ToString("hh:MM:ss"));
    }

    // something made up, what might work in console but not under IIS (SynchronizationContext is special!)
    private IActionResult DoDelaySyncIncorrect() {
      var task = DoDelayAsync();
      var aw = task.GetAwaiter();
      while(!aw.IsCompleted)
        System.Threading.Thread.Yield();
      return AsPlainText("OK");
    }

    private async Task DoDelayAsync() {
      await Task.Delay(1000);
    }

    private IActionResult AsPlainText(string value) {
      return Content(value, "text/plain");
    }
    #endregion

  }//class
}
