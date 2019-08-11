using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Vita.Entities.Logging {
  using System.Linq;

  public static class LogExtensions {

    public static ErrorLogEntry LogError(this ILog log, Exception exc, LogContext context = null) {
      var ee = new ErrorLogEntry(context, exc);
      log.AddEntry(ee);
      return ee; 
    }
    public static void LogError(this ILog log, string message) {
      log.AddEntry(new ErrorLogEntry(LogContext.SystemLogContext, message, null));
    }
    public static void WriteMessage(this ILog log, string message, params object[] args) {
      var msg = Util.SafeFormat(message, args);
      log.AddEntry(new InfoLogEntry(LogContext.SystemLogContext, msg, null));
    }


    public static string GetAllAsText(this IBufferingLog log) {
      var text = string.Join(Environment.NewLine, log.GetAll());
      return text; 
    }

    public static bool HasErrors (this IBufferingLog log) {
      var hasErrors = log.GetAll().Any(e => e.EntryType == LogEntryType.Error);
      return hasErrors;
    }

    public static void CheckErrors(this IBufferingLog log, string message) {
      if (log.ErrorCount > 0) {
        var bufLog = log as IBufferingLog; 
        var errors = bufLog?.GetAllAsText() ?? "See details in error log.";
        System.Diagnostics.Trace.WriteLine(message + Environment.NewLine + errors);
        log.Flush(); 
        throw new StartupFailureException(message, errors);
      }
    }

  }
}
