using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Logging {

  public abstract class LogEntry {
    public Guid Id;
    public DateTime CreatedOn;
    public LogContext Context;

    public LogEntry() {
      Id = Guid.NewGuid(); 
      CreatedOn = TimeService.Instance.UtcNow; 
    }

    public LogEntry(LogContext context) : this() {
      Context = context;
    }

    public virtual bool IsError => false;
    // BatchGroup is used by log batching service to group log entries before sending them 
    // out to save in different tables. It is identical to entry's type in most cases, except 
    // for DbCommandLogEntry - it returns 
    public virtual Type BatchGroup => this.GetType(); 

    public abstract string AsText();

    public override string ToString() {
      return AsText();
    }
  }

  /// <summary>
  /// Basic information about log entry - user, session, ets. May be shared between multiple log entries. 
  /// </summary>
  public class LogContext {
    public static LogContext SystemLogContext = new LogContext() { User = UserInfo.System };

    public UserInfo User;
    public Guid? SessionId;
    public Guid? WebCallId;

    public LogContext() { }

    public LogContext(OperationContext opContext) {
      User = opContext.User;
      SessionId = opContext.UserSession?.SessionId;
      WebCallId = opContext.WebContext?.Id;
    }
  }

}
