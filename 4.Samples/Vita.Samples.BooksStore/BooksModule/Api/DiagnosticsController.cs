using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Web;
using System.IO;
using Vita.Common;

namespace Vita.Samples.BookStore {

  //Special methods: heartbeat, throwerror, get/set time offset
  /// <summary>A controller with special diagnostics functions: heartbeat, throw-error, get/set timeoffset. </summary>
  /// <remarks>We recommend to include have this (or similar) controller in a Web application (including production envrironment). 
  /// It provides diagnostic endpoints: 
  /// <list>
  ///   <item>Heartbeat: an endpoint providing verification that server is up and running; this endpoint might be used in automatic monitoring services (WebSitePulse.com)</item>
  ///   <item>Throw error on demand - to check that error logging functionality is working properly. Hitting on the endpoint throws exception on the server - you then check that it is logged 
  ///          in the error log.</item>
  ///   <item>Get/set time offset - for testing time-dependent functions; ex: expiration time for temp password. Temp password expires in 24 hours, so rather than waiting for a long time
  ///   the tester can simply fast-forward the time by setting the timeoffset - by hitting the set-offset endpoint from a browser. 
  ///   VITA uses internal TimeService for all time inquiries, and this service allows setting an offset value, so that for all internal functions the current time will appear to be shifted. 
  ///   The offset endpoint is enabled only if static bool variable EnableTimeOffset is set to true - this should be set from config file only in test/staging environments.</item>
  /// </list>"
  /// The controller methods return plain text (content-type = application/text), so they can be used directly in a Web browser. 
  /// </remarks>
  [ApiRoutePrefix("diagnostics"), ApiGroup("Diagnostics")]
  public class DiagnosticsController : SlimApiController {
    public static bool EnableTimeOffset;
    static DateTime _lastCalledOn;
    public const string TimeframeLockout = "Denied: wait 5 Seconds";
    public const string StatusOK = "StatusOK";
    public const string StatusFailed = "StatusFailed";
    public const string FunctionNotEnabled = "Function not enabled";
    public static int HeartbeatPauseSeconds = 5;
    public static int ThrowErrorPauseSeconds = 30;

    public static void Reset() {
      _lastCalledOn = new DateTime(2000, 1, 1);
    }

    [ApiGet, ApiRoute("heartbeat")]
    public string GetHeartbeat() {
      Context.LogLevel = Vita.Entities.Services.LogLevel.None;
      Context.WebContext.OutgoingHeaders.Add("Content-type", "text/plain");
      var utcNow = Context.App.TimeService.UtcNow;
      if(_lastCalledOn.AddSeconds(HeartbeatPauseSeconds) > utcNow)
        return AsPlainText(TimeframeLockout);
      _lastCalledOn = utcNow;
      try {
        var session = Context.App.OpenSystemSession();
        var someUser = session.EntitySet<IUser>().FirstOrDefault(); 
        if(someUser == null)
          return AsPlainText(StatusFailed);
        else 
          return AsPlainText(StatusOK);
      } catch(Exception ex) {
        return AsPlainText("Error:" + ex.Message);
      }
    }//method

    private string AsPlainText(string value) {
      Context.WebContext.OutgoingResponseContent = value; 
      return value; 
    }


    [ApiGet, ApiRoute("throwerror")]
    public string ThrowError() {
      var now = DateTime.UtcNow;
      if(_lastCalledOn.AddSeconds(ThrowErrorPauseSeconds) > now)
        return TimeframeLockout;
      _lastCalledOn = now;
      throw new TestException();
    }

    class TestException : Exception {
      public TestException() : base("TestException thrown on request.") { }
    }

    [ApiGet, ApiRoute("timeoffset")]
    public string GetTimeOffset() {
      if(!EnableTimeOffset)
        return AsPlainText(FunctionNotEnabled);
      var utcNow = Context.App.TimeService.UtcNow;
      var offset = (int)Context.App.TimeService.CurrentOffset.TotalMinutes;
      return AsPlainText("Current offset: " + offset + " minutes, current UTC: " + utcNow.ToString("G"));
    }

    [ApiGet, ApiRoute("timeoffset/{minutes}")]
    public string SetTimeOffset(int minutes) {
      if(!EnableTimeOffset)
        return AsPlainText(FunctionNotEnabled);
      Context.App.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(minutes));
      return GetTimeOffset();
    }

  }//class
}//ns
