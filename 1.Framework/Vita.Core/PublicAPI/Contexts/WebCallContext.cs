using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Entities.Web {

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

  /// <summary>Provides an access to web-related parameters to the middle-tier code.</summary>
  /// <remarks>
  /// Instantiated by WebCallContextHandler; the current instance is available through OperationContext.WebContext property. 
  /// WebCallContext provides access to cookies, headers, URL of the incoming request and allows to set specific HTTP response
  /// status on the response. It allows setting response HTTP headers and cookies right from the middle-tier code. 
  /// WebCallContext also serves as a container for log information which can be saved in the database upon completion 
  /// of the request processing. 
  /// Classic (non-Slim version) Web Api controllers can retrieve an instance of the WebCallContext from the request properties 
  /// using <c>WebCallContextKey.</c>
  /// </remarks>
  public class WebCallContext {
    // Used as key to save in Request properties. 
    public const string WebCallContextKey = "_vita_web_call_context_";

    public readonly Guid Id;
    public readonly DateTime CreatedOn; 
    public long TickCountStart;
    public long TickCountEnd;
    public string Referrer;
    public string IPAddress;
    public string ControllerName;
    public string MethodName;
    //Request/response objects
    public object RequestMessage { get; set; } //In running code should be HttpRequestMessage instance
    public object ResponseMessage { get; set; } //In running code should be HttpResponseMessage instance 

    //Request parameters
    public string HttpMethod;
    public string RequestUrl;
    public string RequestUrlTemplate;
    public string RequestHeaders;
    public string RequestBody;
    public long? RequestSize;
    public Func<string, IList<Cookie>> GetIncomingCookies { get; private set; }
    public Func<string, IList<string>> GetIncomingHeader { get; private set; }
    //User session
    public string UserSessionToken; //from authentication header/cookie
    public long MinUserSessionVersion; // from X-Versions header
    public long MinCacheVersion; //from X-Versions header, min entity cache version required for request
    
    // Use it to specify response status other than OK(200) in controller. 
    // For example, a typical status for POST would be 201 (Created) - meaning we created new record(s)
    public HttpStatusCode? OutgoingResponseStatus;
    public IList<Cookie> OutgoingCookies = new List<Cookie>();
    public IDictionary<string, string> OutgoingHeaders = new Dictionary<string, string>();

    //two app-specific numbers, use it for your own purpose, to specify the 'app-relevant' info load
    public int RequestObjectCount; //arbitrary, app-specific count of 'important' objects
    public int ResponseObjectCount; //arbitrary, app-specific count of 'important' objects

    public Exception Exception;
    //Response log information
    public Guid? ErrorLogId;
    public HttpStatusCode HttpStatus;
    public string ResponseHeaders;
    public string ResponseBody;
    public long? ResponseSize;


    public OperationContext OperationContext;
    public WebCallFlags Flags;
    // list of custom indicators for use by app that will be saved in web call log
    public IList<string> CustomTags = new List<string>(); 

    public WebCallContext(object request, DateTime receivedOn, long tickCountStart, 
                          Func<string, IList<Cookie>> cookieGetter, Func<string, IList<string>> headerGetter) {
      RequestMessage = request; 
      Id = Guid.NewGuid();
      CreatedOn = receivedOn;
      TickCountStart = tickCountStart;
      GetIncomingCookies = cookieGetter ?? (x => null); 
      GetIncomingHeader = headerGetter ?? (x => null); 
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
