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
    NoLogRequestBody = 1 << 2, 
    NoLogResponseBody = 1 << 3,
  }

  public class RequestInfo {
    public DateTime ReceivedOn;
    public long StartTimestamp; 
    public string HttpMethod;
    public string UrlTemplate;
    public string Url;
    public IDictionary<string, string> Headers;

    public string Body;
    public long? ContentSize;
    public string ContentType;
    public string IPAddress;

    public Exception Exception;
    public string HandlerControllerName;
    public string HandlerMethodName;

    public WeakReference HttpContextRef;
  }

  public class ResponseInfo {
    public int DurationMs;
    public HttpStatusCode HttpStatus = HttpStatusCode.OK;
    public IDictionary<string, string> Headers = new Dictionary<string, string>();
    public int Size; 
    public object Body;
    public string BodyContentType;
    public string OperationLog;
  }



  /// <summary>Provides an access to web-related parameters to the middle-tier code.</summary>
  /// <remarks>
  /// </remarks>
  public class WebCallContext {
    public readonly Guid Id = Guid.NewGuid(); 
    // Used as key to save in Request properties. 
    public const string WebCallContextKey = "_vita_web_call_context_";
    public OperationContext OperationContext;

    public Stream OriginalResponseStream;

    public WebCallLogEntry LogEntry; 

    public long TickCountStart;

    public RequestInfo Request;
    public ResponseInfo Response; 

    public WebCallFlags Flags;

    public WebCallContext(OperationContext opContext, RequestInfo request) {
      this.OperationContext = opContext;
      opContext.WebContext = this;
      Request = request; 
      LogEntry = new WebCallLogEntry(this);
      TickCountStart = Util.GetTimestamp();
    }

  
  }//class

  public static class WebCallContextExtensions {

    public static bool IsSet(this WebCallFlags flags, WebCallFlags flag) {
      return (flags & flag) != 0;
    }

    public static void MarkConfidential(this WebCallContext context) {
      context.Flags |= WebCallFlags.Confidential;
    }

    public static void SetResponseHeader(this WebCallContext context, string name, string value) {
      context.EnsureResponseInfo();
      context.Response.Headers[name] = value;
    }

    public static void SetResponse(this WebCallContext context, object body = null, string contentType = "text/plain",
                                      HttpStatusCode? status = null) {
      context.EnsureResponseInfo();
      if (body != null) {
        context.Response.Body = body;
        context.Response.BodyContentType = contentType;
      }
      if (status != null)
        context.Response.HttpStatus = status.Value;
    }

    public static void EnsureResponseInfo(this WebCallContext context) {
      if (context.Response == null)
        context.Response = new ResponseInfo();
    }

  } //class
}
