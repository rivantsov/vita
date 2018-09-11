using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public class LogEntryContextInfo {
    public string UserName;
    public Guid? UserId;
    public Guid? UserSessionId;
    public Guid? WebCallId;
    public ProcessType ProcessType;
    public Guid? ProcessId;

    public LogEntryContextInfo() { }

    public LogEntryContextInfo(OperationContext context) {
      UserName = context.User.UserName;
      UserId = context.User.UserId;
      UserSessionId = context.SessionId;
      WebCallId = context.WebContext?.Id;
      ProcessType = context.ProcessType;
      ProcessId = context.ProcessId; 
    }

  }
}
