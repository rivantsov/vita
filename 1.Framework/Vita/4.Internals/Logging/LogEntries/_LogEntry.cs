using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Logging {

  public abstract class LogEntry {
    public Guid? Id;
    public LogEntryType EntryType;
    public DateTime CreatedOn;
    public LogContext Context;

    public LogEntry(LogEntryType entryType) {
      EntryType = entryType;
      CreatedOn = TimeService.Instance.UtcNow; 
    }

    public LogEntry(LogEntryType entryType, LogContext context) : this(entryType) {
      Context = context;
    }

    public abstract string AsText();
  }

  // some information shared between multiple log entries; copied from OperationContext
  public class LogContext {
    public string UserName;
    public Guid? UserId;
    public long AltUserId; 
    public Guid? UserSessionId;
    public Guid? WebCallId;
    public ProcessType ProcessType;
    public Guid? ProcessId;

    public LogContext() { }

    public LogContext(OperationContext context) {
      UserName = context.User.UserName;
      UserId = context.User.UserId;
      UserSessionId = context.SessionId;
      WebCallId = context.WebContext?.Id;
      ProcessType = context.ProcessType;
      ProcessId = context.ProcessId;
    }

  }

}
