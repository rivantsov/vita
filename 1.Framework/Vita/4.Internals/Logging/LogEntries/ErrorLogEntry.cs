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
    public Type ExceptionType;
    public ErrorKind Kind;
    public DateTime? RemoteTime;

    public ErrorLogEntry(LogContext context, Exception exception) 
           : base(LogEntryType.Error, context) {
      Message = exception.Message; 
      Details = exception.ToLogString();
      ExceptionType = exception.GetType();
    }

    public ErrorLogEntry(LogContext context, string message, string details, ErrorKind kind = ErrorKind.Internal, DateTime? remoteTime = null) 
           : base(LogEntryType.Error, context) {
      Message = message;
      Details = details;
      Kind = kind; 
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
