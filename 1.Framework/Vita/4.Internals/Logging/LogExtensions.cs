using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Vita.Entities.Logging {

  public static class LogExtensions {

    public static ErrorLogEntry LogError(this ILog log, Exception exc, OperationContext context = null) {
      var ee = new ErrorLogEntry(context, exc);
      log.AddEntry(ee);
      return ee; 
    }
    public static ErrorLogEntry LogRemoteError(this ILog log, string message, string details, string appName = null, 
                                    string userName = null, DateTime? remoteTime = null, Guid? id = null) {
      var entry = new ErrorLogEntry(null, message, details, ErrorKind.Remote, appName, userName, remoteTime);
      if(id != null)
        entry.Id = id.Value; 
      log.AddEntry(entry);
      return entry;
    }


    public static DbCommandInfoLogEntry LogDbCommand(this ILog log, IDbCommand command, long executionTime, int rowCount) {
      var entry = new DbCommandInfoLogEntry(command, executionTime, rowCount);
      log.AddEntry(entry);
      return entry; 
    }

    public static EventLogEntry LogEvent(this ILog log, string category, string eventType, EventSeverity severity, string message, string details = null,
                         Guid? objectId = null, string objectName = null, int? intParam = null, OperationContext context = null) {
      var entry = new EventLogEntry(category, eventType, severity, message, details, objectId, objectName, intParam, context);
      log.AddEntry(entry);
      return entry; 
    }

  }
}
