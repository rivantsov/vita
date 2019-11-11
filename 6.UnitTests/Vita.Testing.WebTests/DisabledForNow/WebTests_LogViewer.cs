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
using Vita.UnitTests.Common;
using Vita.Modules.Logging;
using Vita.Samples.BookStore.Api;
using Vita.Modules.Logging.Api;
using Vita.Modules.WebClient;
using Vita.Modules.WebClient.Sync;

namespace Vita.UnitTests.Web {
  public partial class WebTests {

    [TestMethod]
    public void TestLogViewerApi() {
      // Here we test Vita.Modules.Logging.Api.LoggingDataController functions 
      var client = Startup.Client;
      // Make two calls, a good one, and one with server error; then try to retrieve the logs
      var vbBooks = client.Get<SearchResults<Book>>("api/books?titlestart={0}", "vb");
      Assert.IsTrue(vbBooks.Results.Count > 0, "Failed to find VB book");
      var exc = TestUtil.ExpectFailWith<Exception>(() => client.Get<string>("api/special/nullref"));
      Assert.IsNotNull(exc, "Expected exception");
      //Flush all logs - they are usually flushed every second
      Startup.FlushLogs();

      // Login as Kevin, he is site admin
      this.LoginAs("Kevin");
      var errors = client.Get<SearchResults<ErrorData>>("api/logs/errors?take=1"); //find last error
      Assert.IsTrue(errors.Results.Count > 0, "Expected at least 1 error");
      var lastErr = errors.Results[0];
      Assert.AreEqual("NullReferenceException", lastErr.ExceptionType, "Expected null-ref exc");
      // we used 'search' call which brings list of errors without details; let's get data by ID - now with call stack
      Assert.IsTrue(string.IsNullOrWhiteSpace(lastErr.Details), "Expected no details");
      var lastErrD = client.Get<ErrorData>("api/logs/errors/{0}", lastErr.Id);
      Assert.IsNotNull(lastErrD, "Failed to get error details.");
      Assert.IsFalse(string.IsNullOrWhiteSpace(lastErrD.Details), "Expected details");
      // Let's get WebCall data for this error
      var webCallData = client.Get<WebCallLogData>("api/logs/webcalls/{0}", lastErr.WebCallId);
      Assert.IsNotNull(webCallData, "Failed to get web call data.");
      Assert.IsTrue(webCallData.Url.EndsWith("api/special/nullref"), "Expected null-ref URL");

      Logout();

      // login as Diego - he is not site admin, so he cannot access logs
      this.LoginAs("Diego");
      var exc2 = TestUtil.ExpectFailWith<ApiException>(() => client.Get<SearchResults<ErrorData>>("api/logs/errors?take=1")); //access denied
      Assert.IsNotNull(exc2, "Expected access denied exception");
      Assert.AreEqual(HttpStatusCode.Forbidden, exc2.Status, "Expected Forbidden status");
      Logout();

    }

    [TestMethod]
    public void TestEventLog() {
      var client = Startup.Client;
      var time = Startup.BooksApp.TimeService; 
      // 'api/events/public' is an endpoint for sending event data if there's no user logged in. 
      // it is enabled if we register EventsPost controller
      var clickEvt = new EventData() {
        Id = Guid.NewGuid(), EventType = "BannerClick", StartedOn = time.UtcNow.AddSeconds(-1), Duration = 1,
        Location = "somewhere", Value = 1.0, StringValue = "Touch", Parameters = new Dictionary<string, string>()
      };
      clickEvt.Parameters["Param1"] = "Value1";
      var status = client.Post<EventData[], HttpStatusCode>(new []{clickEvt}, "api/logs-post/events/public");

      //Login as dora, try other endpoint for posting events for logged in users
      LoginAs("dora");
      var clickEvt2 = new EventData() {
        Id = Guid.NewGuid(), EventType = "BannerClick", StartedOn = time.UtcNow.AddSeconds(-1), Duration = 1,
        Location = "somewhere", Value = 1.0, StringValue = "Touch", Parameters = new Dictionary<string, string>()
      };
      clickEvt2.Parameters["Param2"] = "Value2";
      status = client.Post<EventData[], HttpStatusCode>(new [] {clickEvt2}, "api/logs-post/events");
      Logout(); 

      // let's search and find this event. We need to be logged in as site admin
      // Login as Kevin, he is site admin
      Startup.BooksApp.Flush(); //force flush it
      this.LoginAs("Kevin");
      var events = client.Get<SearchResults<EventData>>("api/logs/events?eventtype={0}",  "BannerClick");
      Assert.IsTrue(events.Results.Count >= 2, "Expected to find an event");
      Assert.AreEqual("BannerClick", events.Results[0].EventType, "Expected BannerClick event type.");
      Assert.AreEqual("BannerClick", events.Results[1].EventType, "Expected BannerClick event type.");
      // by default events are sorted by StartedOn-DESC, so first submitted is the last
      var lastEvt = events.Results[1];
      //get event details
      var evt1 = client.Get<EventData>("api/logs/events/{0}", lastEvt.Id);
      Assert.IsNotNull(evt1, "Expected event object by Id");
      Assert.AreEqual(1, evt1.Parameters.Count, "Expected one parameter");
      Assert.AreEqual("Value1", evt1.Parameters["Param1"], "Wrong param value.");
      Logout(); 
    }
  }
}
