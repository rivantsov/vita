using System;
using System.Collections.Generic;
using System.Diagnostics;
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

  public interface IHttpContextWrapper {
    object HttpContext { get; }
  }

  public struct RequestInfo {
    public string HttpMethod;
    public string Url;
    public string UrlHost;
    public string UrlPath; 
    public IDictionary<string, string> Headers;

    public long? ContentSize;
    public string ContentType; 
    public string IPAddress;
  }

  public class ResponseInfo {
    public HttpStatusCode? HttpStatus;
    public IDictionary<string, string> Headers = new Dictionary<string, string>();
    public string ContentType;
    public object Body;
  }

  /// <summary>Provides an access to web-related parameters to the middle-tier code.</summary>
  /// <remarks>
  /// Instantiated by WebCallContextHandler; the current instance is available through OperationContext.WebContext property. 
  /// WebCallContext provides access to cookies, headers, URL of the incoming request and allows to set specific HTTP response
  /// status on the response. It allows setting response HTTP headers and cookies right from the middle-tier code. 
  /// WebCallContext also serves as a container for log information which can be saved in the database upon completion 
  /// of the request processing. 
  /// Classic (non-Slim version) Web Api controllers can retrieve an instance of the WebCallContext from the Request object properties 
  /// using <c>WebCallContextKey</c> as a key.
  /// </remarks>
  public class WebCallContext {
    // Used as key to save in Request properties. 
    public const string WebCallContextKey = "_vita_web_call_context_";
    public OperationContext OperationContext;

    public readonly WeakReference HttpContextRef;

    public readonly Guid Id;
    public readonly DateTime CreatedOn; 
    public long TickCountStart;
    public long TickCountEnd;
    public int Duration;
    public Exception Exception;
    public Guid? ErrorLogId;

    public RequestInfo Request;
    public ResponseInfo Response; 

    public string RequestUrlTemplate;
    public string ControllerName;
    public string MethodName;


    public WebCallFlags Flags;
    // list of custom indicators for use by app that will be saved in web call log
    public IList<string> CustomTags = new List<string>(); 

    public WebCallContext(OperationContext opContext) {
      this.OperationContext = opContext;
      opContext.WebContext = this; 
    }

    public WebCallContext(OperationContext opContext, object httpContext, RequestInfo request) {
      this.OperationContext = opContext;
      HttpContextRef = new WeakReference(httpContext); 
      Id = Guid.NewGuid();
      CreatedOn = this.OperationContext.App.TimeService.UtcNow;
      TickCountStart = Stopwatch.GetTimestamp();
      Request = request; 
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
