using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Entities.Logging {

  public enum LogEntryType {
    Information,
    Command,
    Error,
    System,
  }

  //base class
  public abstract class LogEntry {
    public Guid? Id;
    public LogEntryType EntryType;
    public DateTime CreatedOn;
    public string UserName;
    public Guid? UserSessionId;
    public Guid? WebCallId;
    public LogEntry(OperationContext context, LogEntryType entryType, DateTime? createdOn = null) {
      EntryType = entryType; 
      if(context != null) {
        CreatedOn = createdOn == null ? context.App.TimeService.UtcNow : createdOn.Value;
        UserName = context.User.UserName;
        if(context.UserSession != null)
          UserSessionId = context.UserSession.SessionId;
        if(context.WebContext != null)
          WebCallId = context.WebContext.Id;
      }
    }
  }

  public class InfoLogEntry : LogEntry {
    string _message;
    object[] _args;
    string _formattedMessage;
    
    public InfoLogEntry(OperationContext context, string message, params object[] args) : base(context, LogEntryType.Information) {
      _message = message;
      _args = args;
    }

    public override string ToString() {
      if (_formattedMessage == null)
        _formattedMessage = StringHelper.SafeFormat(_message, _args);
      return _formattedMessage;
    }
  }//class

  public class ErrorLogEntry : LogEntry {
    public Exception Exception;
    string _message;
    object[] _args;
    string _formattedMessage;

    public ErrorLogEntry(OperationContext context, Exception exception): base(context, LogEntryType.Error) {
      Exception = exception;
    }
    public ErrorLogEntry(OperationContext context, string message, params object[] args) : base(context, LogEntryType.Error) {
      _message = message;
      _args = args; 
    }

    public override string ToString() {
      if (_formattedMessage == null) {
        if(Exception != null)
          _formattedMessage = StringHelper.SafeFormat("!!! Exception: \r\n {0}", this.Exception.ToLogString());
        else
          _formattedMessage = StringHelper.SafeFormat(_message, _args); 

      }
      return _formattedMessage; 
    }
  }//class

}
