using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Entities.Logging
{
  public class WebCallLogEntry : LogEntry {
    public int Duration;
    public string Url;
    public string UrlTemplate;
    public string UrlReferrer;
    public string IPAddress;
    public string ControllerName;
    public string MethodName;
    //Request
    public string HttpMethod;
    public WebCallFlags Flags;
    public string CustomTags;
    public string RequestHeaders;
    public string RequestBody;
    public long? RequestSize;
    public int RequestObjectCount; //arbitrary, app-specific count of 'important' objects

    //Response
    public HttpStatusCode HttpStatus;
    public string ResponseHeaders;
    public string ResponseBody;
    public long? ResponseSize;
    public int ResponseObjectCount; //arbitrary, app-specific count of 'important' objects

    //log and exceptions
    public Guid? ErrorLogId;
    public string Error;
    public string ErrorDetails;

    public WebCallLogEntry(WebCallContext webCtx) : base(webCtx.OperationContext, LogEntryType.WebCall) {
      var ctx = webCtx.OperationContext;
      this.Id = webCtx.Id;
      this.CreatedOn = webCtx.CreatedOn;
      this.Duration = (int)(webCtx.TickCountEnd - webCtx.TickCountStart);
      this.Url = webCtx.RequestUrl;
      this.UrlTemplate = webCtx.RequestUrlTemplate;
      this.UrlReferrer = webCtx.Referrer;
      this.IPAddress = webCtx.IPAddress;
      this.ControllerName = webCtx.ControllerName;
      this.MethodName = webCtx.MethodName;
      if(webCtx.Exception != null) {
        this.Error = webCtx.Exception.Message;
        this.ErrorDetails = webCtx.Exception.ToLogString();
      }
      this.ErrorLogId = webCtx.ErrorLogId;
      this.HttpMethod = webCtx.HttpMethod;
      this.HttpStatus = webCtx.HttpStatus;
      this.RequestSize = webCtx.RequestSize;
      this.RequestHeaders = webCtx.RequestHeaders;
      this.Flags = webCtx.Flags;
      if(webCtx.CustomTags.Count > 0)
        this.CustomTags = string.Join(",", webCtx.CustomTags);
      if(webCtx.Flags.IsSet(WebCallFlags.HideRequestBody))
        this.RequestBody = "(Omitted)";
      else
        this.RequestBody = webCtx.RequestBody;

      this.ResponseSize = webCtx.ResponseSize;
      this.ResponseHeaders = webCtx.ResponseHeaders;
      if(webCtx.Flags.IsSet(WebCallFlags.HideResponseBody))
        this.ResponseBody = "(Omitted)";
      else
        this.ResponseBody = webCtx.ResponseBody;
      this.RequestObjectCount = webCtx.RequestObjectCount;
      this.ResponseObjectCount = webCtx.ResponseObjectCount;
    }

    public override string AsText() {
      return $"{HttpMethod} {Url}";
    }

    public override string ToString() {
      return AsText();
    }
  }
}
