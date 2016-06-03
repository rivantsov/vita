using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.Api;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace Vita.UnitTests.Web {

  // Service controller with special methods for testing error handling
  [ApiRoutePrefix("special")]
  public class SpecialMethodsController : SlimApiController {

    // Throws null ref exception
    [ApiGet, ApiRoute("nullref")]
    public string ThrowNullRef() {
      //Before we throw, let's write smth to local log; 
      Context.LocalLog.Info("Throwing NullReference exception...");
      string foo = null;
      if(foo.Length > 10)
        foo = "bar";
      return foo; 
    }

    // Illustrates/tests returning custom HTTP status code.
    // In real apps, NotFound should be left to situations when endpoint is not found (bad URL, page or api method does not exist).
    [ApiGet, ApiRoute("books/{id}")]
    public Book GetBook(Guid id) {
      var session = Context.OpenSystemSession();
      var bk = session.GetEntity<IBook>(id);
      if(bk == null) {
        Context.WebContext.OutgoingResponseStatus = HttpStatusCode.NotFound;
        return null;
      }
      return bk.ToModel(details: true);
    }


    // this method is used for testing handling error of bad parameter types
    [ApiGet, ApiRoute("foo/{p1}/{p2}")]
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
    [ApiGet, ApiRoute("bar")]
    public string Bar([FromUrl] BarParams prms, OperationContext context) {
      Util.Check(context != null, "OperationContext not inserted.");
      return prms.P1 + prms.P2;
    }

    // method requires login
    [ApiGet, ApiRoute("bars"), LoggedInOnly]
    public string Bars(string q) {
      return "bars:" + q;
    }

    [ApiPost, ApiRoute("sessionvalue")]
    public void SetSessionValue(string name, string value) {
      Context.UserSession.SetValue(name, value); 
    }
    [ApiGet, ApiRoute("sessionvalue")]
    public string GetSessionValue(string name) {
      return Context.UserSession.GetValue<string>(name);
    }


    //See comments in TestDbConnectionHandling for details
    static string _connectionCloseReport;
    [ApiGet, ApiRoute("connectiontest")]
    public string TestConnectionHandling() {
      if(Context.DbConnectionMode != DbConnectionReuseMode.KeepOpen)
        return "Error: Connection mode is not KeepOpen. Mode: " + Context.DbConnectionMode;
      _connectionCloseReport = "(empty)"; 
      var session = Context.OpenSystemSession();
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
      bool calledConnClose = callStack.Contains("Vita.Data.DataConnection.Close()");
      bool calledPostProcessResponse = callStack.Contains("Vita.Web.WebCallContextHandler.PostProcessResponse(");
      _connectionCloseReport = string.Format("{0},{1},{2}", isClosed, calledConnClose, calledPostProcessResponse);
    }

    [ApiGet, ApiRoute("connectiontestreport")]
    public string GetConnectionnTestReport() {
      return _connectionCloseReport;
    }

    // Testing DateTime values in URL. It turns out by default WebApi uses 'model binding' for URL parameters, which 
    // results in datetime in URL (sent as UTC) to be converted to local datetime (local for server). 
    // This is inconsistent with DateTime values in body (json) - by default NewtonSoft deserializer treats them as UTC values
    // VITA provides a fix - it automatcally detects local datetimes and converts them to UTC
    // The test sends the datetime parameter to server and receives the ToString() representation; then it compares it to original. 
    // With VITA's fix, they should be identical
    [ApiGet, ApiRoute("datetostring")]
    public string GetUrlDateToString(DateTime dt) {
      return dt.ToString("u");
    }

    // this is a variation, when datetime is nullable in URL - typical case for search queries
    public class DateBox {
      public DateTime? Date { get; set; }
    }
    [ApiGet, ApiRoute("datetostring2")]
    public string GetUrlDateToString([FromUrl] DateBox dateBox) {
      var dt = dateBox.Date; 
      return dt == null ? string.Empty : dt.Value.ToString("u");
    }

    [ApiGet, ApiRoute("getdateasync")]
    public async Task<string> GetDateAsync() {
      var dt = this.Context.App.TimeService.UtcNow;
      return await Task.FromResult(dt.ToString("u"));
    }

    //Testing fix - calling method with redirect was failing in attempt to deserialize response
    [ApiGet, ApiRoute("redirect")]
    public void RedirectToSearch() {
      var webCtx = Context.WebContext;
      webCtx.OutgoingResponseStatus = System.Net.HttpStatusCode.Redirect;
      webCtx.OutgoingHeaders.Add("Location", "http://www.google.com");
    }

  }//class
}
