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
    public string UserName;
    public IBufferedLog Log; 
    public RequestInfo Request;
    public ResponseInfo Response;
    public WebCallFlags Flags;

    public Guid? ErrorLogId;
    public Exception Exception;
    string _logContents; 

    //log and exceptions

    public WebCallLogEntry(WebCallContext webCtx) : base(webCtx.OperationContext.LogContext) {
      WebCallId = webCtx.Id;
      UserName = webCtx.OperationContext.User?.UserName;
      Log = webCtx.OperationContext.Log as IBufferedLog;
      Request = webCtx.Request;
      Response = webCtx.Response;
      Flags = webCtx.Flags;
      Exception = webCtx.Request?.Exception;
    }

    public override string ToString() {
      return $"{Request.HttpMethod}: {Request.Url}";
    }

    public override string AsText() {
      var log = GetLogContents();
      string err = null;
      if (this.Exception != null)
        err = this.Exception.ToLogString();
      var text = $@"
---------------------------------------------------------------------------------------
{Request.HttpMethod}: {Request.Url}
{Request.Body}
User: {UserName}
{log}
{err}
Response status: {Response.HttpStatus}, bytes: {Response.Size}, duration: {Response.DurationMs} ms
";
      return text; 
    }

    public string GetLogContents() {
      if (_logContents == null)
        _logContents = this.Log?.GetAllAsText();
      return _logContents;
    }
  }
}
