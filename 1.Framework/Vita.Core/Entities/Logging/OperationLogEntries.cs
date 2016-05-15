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
  // Note: there are more LogEntry-derived classes in other parts of framework (DbCommandLogEntry, CacheCommandLogEntry, etc)
  public abstract class OperationLogEntry : LogEntry {
    public LogEntryType EntryType;

    protected OperationLogEntry(LogEntryType entryType, OperationContext context, DateTime? createdOn = null) : base(context, createdOn) {
      EntryType = entryType;
    }
  }

  public class InfoLogEntry : OperationLogEntry {
    string _message;
    object[] _args;
    string _formattedMessage;
    
    public InfoLogEntry(OperationContext context, string message, params object[] args) : base(LogEntryType.Information, context) {
      _message = message;
      _args = args;
    }

    public override string ToString() {
      if (_formattedMessage == null)
        _formattedMessage = StringHelper.SafeFormat(_message, _args);
      return _formattedMessage;
    }
  }//class

  public class ErrorLogEntry : OperationLogEntry {
    public Exception Exception;
    string _message;
    object[] _args;
    string _formattedMessage;

    public ErrorLogEntry(OperationContext context, Exception exception): base(LogEntryType.Error, context) {
      Exception = exception;
    }
    public ErrorLogEntry(OperationContext context, string message, params object[] args) : base(LogEntryType.Error, context) {
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

  /// <summary>System log entry message. When written to operation log, each entry appears in separate record. </summary>
  public class SystemLogEntry : InfoLogEntry {
    public SystemLogEntry(OperationContext context, string message, params object[] args) : base(context, message, args) {
      EntryType = LogEntryType.System;
    }
  }

}
