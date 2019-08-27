using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Services;

namespace Vita.Entities.Api {

  [Flags]
  public enum WebCallFlags {
    None = 0,
    Confidential = 1,
    UsedCache = 1 << 1, 

    NoLogRequestBody = 1 << 4, 
    NoLogResponseBody = 1 << 5,

    CancelProcessing = 1 << 8,
    AttackRedFlag = 1 << 9,
    AttackInProgress = 1 << 10,
    AttackClearAll = 1 << 11, // clear all attack red flags

    HideRequestBody = Confidential | NoLogRequestBody,
    HideResponseBody = Confidential | NoLogResponseBody,
  }

  public class RequestInfo {
    public DateTime ReceivedOn; 
    public string HttpMethod;
    public string UrlTemplate;
    public string Url;
    public IDictionary<string, string> Headers;

    public object Body;
    public long? ContentSize;
    public string ContentType;
    public string IPAddress;

    public WeakReference HttpContextRef;
  }

  public class ResponseInfo {
    public string ControllerName;
    public string MethodName;
    public TimeSpan Duration;
    public HttpStatusCode HttpStatus = HttpStatusCode.OK;
    public IDictionary<string, string> Headers = new Dictionary<string, string>();
    public object Body;
    public string BodyContentType;
  }



  /// <summary>Provides an access to web-related parameters to the middle-tier code.</summary>
  /// <remarks>
  /// </remarks>
  public class WebCallContext {
    // Used as key to save in Request properties. 
    public const string WebCallContextKey = "_vita_web_call_context_";
    public OperationContext OperationContext;

    public Stream OriginalResponseStream;

    public WebCallLogEntry LogEntry; 

    public long TickCountStart;

    public RequestInfo Request;
    public ResponseInfo Response; 

    public WebCallFlags Flags;
    // list of custom indicators for use by app that will be saved in web call log
    public IList<string> CustomTags = new List<string>(); 

    public WebCallContext(OperationContext opContext, RequestInfo request) {
      this.OperationContext = opContext;
      opContext.WebContext = this;
      Request = request; 
      LogEntry = new WebCallLogEntry(this);
      TickCountStart = AppTime.GetTimestamp();
    }

  
  }//class

  public static class WebCallContextExtensions {
    public static bool IsSet(this WebCallFlags flags, WebCallFlags flag) {
      return (flags & flag) != 0;
    }
    public static void MarkConfidential(this WebCallContext context) {
      context.Flags |= WebCallFlags.Confidential;
    }

  }
}
