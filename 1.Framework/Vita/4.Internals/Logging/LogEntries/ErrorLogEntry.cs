using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public enum ErrorKind {
    /// <summary>Regular error/exception.</summary>
    Internal,
    /// <summary>Error in remote subsystem posted to the server; for example, error in UI script.</summary>
    Remote, 
  }

  public class ErrorLogEntry : LogEntry {
    public string Message;
    public string Details;
    public string LogContents;
    public Type ExceptionType;
    public string AppName;
    public ErrorKind Kind;
    public DateTime? RemoteTime;
    public string RemoteUserName;

    public ErrorLogEntry(OperationContext context, Exception exception, string header = null) : base(context, LogEntryType.Error) {
      Message = header + exception.Message; 
      Details = exception.ToLogString();
      LogContents = context?.GetLogContents();
      ExceptionType = exception.GetType();
      AppName = context?.App?.AppName;
    }
    public ErrorLogEntry(OperationContext context, string message, string details, ErrorKind kind = ErrorKind.Internal, 
                         string appName = null, string userName = null, DateTime? remoteTime = null) 
           : base(context, LogEntryType.Error) {
      Message = message;
      Details = details;
      Kind = kind; 
      LogContents = context?.GetLogContents();
      AppName = appName ?? context?.App?.AppName;
      this.RemoteUserName = userName;
      RemoteTime = remoteTime; 
    }
    public override string AsText() {
      return Details;
    }
    public override string ToString() {
      return Message;
    }
  }//class

}
