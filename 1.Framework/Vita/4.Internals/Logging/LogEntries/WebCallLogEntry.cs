using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Entities.Logging {

  public class WebCallLogEntry : LogEntry {
    public Guid? WebCallId;
    public RequestInfo Request;
    public ResponseInfo Response;
    public WebCallFlags Flags;

    public string ControllerName;
    public string MethodName;
    public TimeSpan Duration;
    public Guid? ErrorLogId;

    //log and exceptions

    public WebCallLogEntry(WebCallContext webCtx, RequestInfo request) 
          : base(LogEntryType.WebCall, webCtx.OperationContext.LogContext) {
      WebCallId = Guid.NewGuid();
      Request = request;
      Response = new ResponseInfo(); 
    }

    public override string AsText() {
      return this.ToString();
    }

    public override string ToString() {
      return Request.ToString();
    }
  }

  public class RequestInfo {
    public string HttpMethod;
    public string UrlTemplate;
    public string Url;
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


}
