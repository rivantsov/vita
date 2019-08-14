using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Entities.Logging {

  public class WebCallLogEntry : LogEntry {
    public Guid? WebCallId;
    public RequestInfo Request;
    public ResponseInfo Response;
    public WebCallFlags Flags;

    public Guid? ErrorLogId;
    public Exception Exception;

    //log and exceptions

    public WebCallLogEntry(WebCallContext webCtx) : base(LogEntryType.WebCall, webCtx.OperationContext.LogContext) {
      WebCallId = Guid.NewGuid();
      Request = webCtx.Request;
      Response = webCtx.Response; 
    }

    public override string AsText() {
      return this.ToString();
    }

    public override string ToString() {
      return Request.ToString();
    }
  }


}
