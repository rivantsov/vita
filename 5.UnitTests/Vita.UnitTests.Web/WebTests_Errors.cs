using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities;
using Vita.Web;
using Vita.Modules.Logging;
using Vita.UnitTests.Common;
using Vita.Samples.BookStore.Api;
using Vita.Samples.BookStore;
using Vita.Modules.Logging.Api;
using Vita.Modules.WebClient.Sync;
using Vita.Modules.WebClient;

namespace Vita.UnitTests.Web {

  public partial class WebTests  {

    // Testing saving client errors (in java script) on the server, using clienterror POST endpoint
    [TestMethod]
    public void TestClientErrorPost() {
      var client = Startup.Client;
      var clientError = new ClientError() {
        Id = Guid.NewGuid(), //optional 
        AppName = "TestApp", Message = "Client Error Message", Details = "Client Error Details",
        LocalTime = Startup.BooksApp.TimeService.Now.AddMinutes(-5) //pretend it happened 5 minutes ago
      };
      var serverErrorId = client.ExecutePost<ClientError, Guid>(clientError, "api/logs-post/clienterrors");
      Assert.AreEqual(clientError.Id, serverErrorId, "Failed to submit client error, IDs do not match");

      //Verify record exists in database
      var serverSession = Startup.BooksApp.OpenSystemSession();
      var errInfo = serverSession.GetEntity<IErrorLog>(serverErrorId);
      Assert.IsNotNull(errInfo, "Failed to get server error record.");
      Assert.AreEqual(clientError.Message, errInfo.Message, "message does not match");
      Assert.AreEqual(clientError.Details, errInfo.Details, "Details do not match");
      Assert.IsTrue(clientError.LocalTime.Value.EqualsTo(errInfo.LocalTime), "Time does not match"); //within 1 ms
    }

    [TestMethod]
    public void TestClientFaults() {
      var client = Startup.Client;
      Logout(); //if we're still logged in from other failed tests

      const string booksUrl = "api/books";
      const string reviewUpdateUrl = "api/user/reviews";
 
      //Get c# books
      var csBooks = client.ExecuteGet<SearchResults<Book>>(booksUrl + "?titlestart={0}", "c#");
      Assert.IsTrue(csBooks.Results.Count > 0, "Failed to find cs book");
      var csBk = csBooks.Results[0]; 


      // Test - anonymous user posts review - server returns BadRequest with AuthenticationRequired fault in the body
      var goodReview = new BookReview() { BookId = csBk.Id, Rating = 5, 
        Caption = "Wow! really awesome!", Review = "Best I ever read about c#!!!" };
      var cfEx = TestUtil.ExpectClientFault(() => client.ExecutePost<BookReview, BookReview>(goodReview, reviewUpdateUrl));
      Assert.AreEqual("AuthenticationRequired", cfEx.Faults[0].Code, "Expected AuthenticationRequired fault.");

      //Login as diego, post again - now should succeed      
      this.LoginAs("Diego");
      var postedReview = client.ExecutePost<BookReview, BookReview>(goodReview, reviewUpdateUrl);
      Assert.IsNotNull(postedReview, "Failed to post review");
      Assert.IsTrue(postedReview.Id != Guid.Empty, "Expected non-empty review ID.");
      
      // Now bad posts - review objects with invalid properties; See CatalogController.PostReview method
      // first try sending null object
      cfEx = TestUtil.ExpectClientFault(() => client.ExecutePost<BookReview, BookReview>(null, reviewUpdateUrl));
      Assert.AreEqual("ContentMissing", cfEx.Faults[0].Code, "Expected content missing fault");

      // Test review with bad properties; first try bad book id; if book id is invalid, controller rejects immediately
      var dummyReview = new BookReview() { BookId = Guid.NewGuid(), Rating = 10 };
      //Should return ObjectNotFound (book not found); 
      cfEx = TestUtil.ExpectClientFault(() => client.ExecutePost<BookReview, BookReview>(dummyReview, reviewUpdateUrl));
      Assert.AreEqual(1, cfEx.Faults.Count, "Should be a single fault.");
      var fault0 = cfEx.Faults[0];
      Assert.IsTrue(fault0.Code == "ObjectNotFound" && fault0.Tag == "BookId");

      // Make new review with bad properties and try to post
      var testReview = new BookReview() { BookId = csBk.Id, Rating = 10, Caption = null, Review = new string('?', 1500)};
      cfEx = TestUtil.ExpectClientFault(() => client.ExecutePost<BookReview, BookReview>(testReview, reviewUpdateUrl));
      // Now we should get multiple faults: Rating out of range, caption may not be null, Review text too long
      /*  Here's BadRequest response body: 
       [{"Code":"ValueMissing","Message":"Caption may not be empty.","Tag":"Caption","Path":null,"Parameters":{}},
        {"Code":"ValueTooLong","Message":"Review text is too long, must be under 1000 chars","Tag":"Review","Path":null,"Parameters":{}},
        {"Code":"ValueOutOfRange","Message":"Rating must be between 1 and 5","Tag":"Rating","Path":null,"Parameters":{"InvalidValue":"10"}}]      
      */
      Assert.AreEqual(3, cfEx.Faults.Count, "Expected 3 faults");
      Assert.IsTrue(cfEx.Faults[0].Code == "ValueMissing" && cfEx.Faults[0].Tag == "Caption", "Expected Caption fault");
      Assert.IsTrue(cfEx.Faults[1].Code == "ValueTooLong" && cfEx.Faults[1].Tag == "Review", "Expected Review too long fault");
      Assert.IsTrue(cfEx.Faults[2].Code == "ValueOutOfRange" && cfEx.Faults[2].Tag == "Rating", "Expected Rating out of range fault");
      Logout();

      // Test invalid URL path, no match to controller method --------------------------------------------------------------------------
      //Test non-existing URL
      var badUrlExc = TestUtil.ExpectFailWith<ApiException>( () => client.ExecuteGet<SearchResults<Book>>("api/non-existing"));
      Assert.AreEqual(HttpStatusCode.NotFound, badUrlExc.Status, "Expected Not found code.");
      // There is a URL, but HTTP method is wrong: nullref method accepts GET only
      var badMethodExc = TestUtil.ExpectFailWith<ApiException>(() => client.ExecutePost<object, string>(null, "api/special/nullref"));
      Assert.AreEqual(HttpStatusCode.NotFound, badMethodExc.Status, "Expected Not found code.");


      // test malformed parameters ------------------------------------------------------------------------------------------------------
      // Check bad parameter type handling
      // p1 should be int, p2 is Guid; we expect BadRequest with 2 faults
      var cfExc = TestUtil.ExpectFailWith<ClientFaultException>(() => client.ExecuteGet<string>("api/special/foo/{0}/{1}", "not-int", "not-Guid"));
      Assert.AreEqual(2, cfExc.Faults.Count, "Expected 2 faults");
    }

    [TestMethod]
    public void TestErrorHandling() {
      var client = Startup.Client;

      // 1. Run op, check that SQL not logged, call method with server error (NullRefExc), check that 501 returned, 
      //  check that error and SQL in WebCall is logged
      // 2. Test access denied exc - Diego tries to delete Dora's review
      // 3. Post malformed Json

      IWebCallLog logEntry;
      var webStt = Startup.BooksApp.GetConfig<WebCallContextHandlerSettings>();
      var savedLogLevel = webStt.LogLevel;
      // Set web log level to Basic - details are not logged unless there's error
      webStt.LogLevel = Entities.Services.LogLevel.Basic;

      // InternalServerError -------------------------------------------------------------------------------------------------
      // We call a server method that throws NullReferenceException; the call will log error on the server,
      // log all web details into web log, and will return InternalServerError, with full error inforamtion in the body 
      // (because we setup service in debug mode, to return error details to client)

      // First let's make a good call that succeeds - check that call details are not logged on server (LogLevel is Basic)
      var allBooks = client.ExecuteGet<SearchResults<Book>>("api/books");
      //Check that details are not logged
      logEntry = GetLastWebLogEntry();
      Assert.IsNotNull(logEntry, "Failed to get last web call log entry");
      Assert.IsTrue(logEntry.Url.Contains("api/books"), "Expected api/books log entry"); //make sure it is correct entry
      Assert.IsTrue(logEntry.ResponseBody == null, "Local log should be empty.");
      Assert.IsTrue(logEntry.LocalLog == null, "Local log should be empty."); //
      //cause null-ref exc on the server
      System.Threading.Thread.Sleep(100); //make pause let time tick, to make sure this call is stamped with latest time
      var exc = TestUtil.ExpectFailWith<ApiException>(() => client.ExecuteGet<string>("api/special/nullref"));
      Assert.AreEqual(HttpStatusCode.InternalServerError, exc.Status, "Expected server error.");
      Assert.IsTrue(exc.Message.Contains("NullReferenceException"), "Expected null reference exception on the server.");
      //Check server-side web log - it should now contain all details, LogLevel is elevated to details
      logEntry = GetLastWebLogEntry();
      var isNullRef = logEntry.Url.Contains("api/special/nullref");
      Assert.IsTrue(isNullRef, "Expected nullref log entry, received: " + logEntry.Url); //make sure it is correct entry
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.ResponseBody), "Response body should not be empty.");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.Error), "Error message should not be empty.");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.ErrorDetails), "Error message should not be empty.");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.LocalLog), "Local log should not be empty."); //
      Assert.IsTrue(logEntry.ErrorLogId != null, "ErrorLogId is null"); //reference to ErrorLog


      // AuthorizationException on the server: Diego tries to delete Dora's review. ------------------------------------------------------------- 
      // This should result in Unauthorized response from the server, with error details logged on the server and returned in response body
      var serverSession = Startup.BooksApp.OpenSession();
      var doraReviewId = serverSession.EntitySet<IBookReview>().First(r => r.User.UserName == "Dora").Id;

      LoginAs("Diego");
      //Check server-side web log - it should be Basic, not details
      logEntry = GetLastWebLogEntry();
      Assert.IsTrue(logEntry.Url.Contains("login"), "Expected login log entry"); //make sure it is Login call entry
      Assert.IsTrue(logEntry.LocalLog == null, "Local log should be empty."); //LogLevel is basic, so for calls without errors no details logged
      //Try to delete Dora's review
      exc = TestUtil.ExpectFailWith<ApiException>(() => client.ExecuteDelete("api/user/reviews/" + doraReviewId));
      Assert.AreEqual(HttpStatusCode.Forbidden, exc.Status, "Expected Forbidden status.");
      //We have web server setup with 'return exc details to client', so response body should contain all details
      // HttpClientWrapper rethrows the exception putting server exception message into the exc it throws. 
      // Server exc details are saved in Data dictionary
      Assert.IsTrue(exc.Message.Contains("Actions(s) [DeleteStrict] denied for record"), "Expected action denied."); //check message fragment
      Assert.IsTrue(!string.IsNullOrWhiteSpace(exc.Details as string), "Expected details");
      //Check server-side web log, details should be logged
      logEntry = GetLastWebLogEntry();
      Assert.IsTrue(logEntry.Url.Contains("review"), "Expected Review log entry"); 
      //check web server log
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.Error), "Error message should not be empty.");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.ErrorDetails), "Error message should not be empty.");
      Assert.IsTrue(logEntry.ErrorLogId != null, "ErrorLogId is null"); //reference to ErrorLog
      Logout();

      // Error deserializing object on the server -------------------------------------------------------------------------------
      // Let's send garbage to server method that expects a nice object in the body.
      LoginAs("Dora");
      var badJson = new StringContent("definitely not Json {/,'{", UTF8Encoding.Default, "application/json");
      var badReqExc = TestUtil.ExpectClientFault(() => client.ExecutePost<HttpContent, HttpResponseMessage>(badJson, "/api/user/reviews"));
      //The HTTP status returned is BadRequest, but in this case the error is logged in details on the server, as a critical error
      logEntry = GetLastWebLogEntry();
      Assert.IsTrue(logEntry.Url.Contains("api/user/reviews"), "Expected review post entry");
      Assert.IsTrue(logEntry.Error != null && logEntry.Error.Contains("Bad request body"), "Expected Bad Request Body");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.ErrorDetails), "Expected saved formatter error");
      Assert.IsTrue(logEntry.ErrorDetails.Contains("Unexpected character encountered"), "Invalid formatter error message");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(logEntry.RequestBody), "Expected detailed log info saved for deserialization error.");

      Logout();

      webStt.LogLevel = savedLogLevel; 
    }

    //returns latest webLogEntry on the server
    private IWebCallLog GetLastWebLogEntry() {
      Startup.FlushLogs();
      var utcNow = Startup.BooksApp.TimeService.UtcNow; 
      var session = Startup.BooksApp.OpenSession();
      var qWL = from wl in session.EntitySet<IWebCallLog>()
                where wl.CreatedOn <= utcNow     // Diagnostics controller test messes up time, so we might have some entries posted in the future - filter them
                  orderby wl.CreatedOn descending
                  select wl;
      var result = qWL.FirstOrDefault();
      return result; 
    }

  }//class
}
