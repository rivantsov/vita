using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Entities.Logging
{
  public class WebCallLogEntry : LogEntry {

    public WebCallContext WebContext; 
    //log and exceptions

    public WebCallLogEntry(WebCallContext webCtx) : base(webCtx.OperationContext, LogEntryType.WebCall) {
      WebContext = webCtx;
    }

    public override string AsText() {
      return this.ToString();
    }

    public override string ToString() {
      return WebContext.ToString();
    }
  }
}
