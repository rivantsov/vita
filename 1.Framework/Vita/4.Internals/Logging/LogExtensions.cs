using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Vita.Entities.Logging {
  using System.Linq;

  public static class LogExtensions {
    private static LogContext SystemLogContext = new LogContext() { User = UserInfo.System };

    public static ErrorLogEntry LogError(this ILog log, Exception exc, LogContext context = null) {
      var ee = new ErrorLogEntry(context, exc);
      log.AddEntry(ee);
      return ee; 
    }
    public static void LogError(this ILog log, string message, params object[] args) {
      var msg = Util.SafeFormat(message, args); 
      log.AddEntry(new ErrorLogEntry(SystemLogContext, msg, null));
    }
    public static void LogInfo(this ILog log, string message, params object[] args) {
      var msg = Util.SafeFormat(message, args);
      log.AddEntry(new InfoLogEntry(SystemLogContext, msg, null));
    }


    public static string GetAllAsText(this IBufferingLog log) {
      var text = string.Join(Environment.NewLine, log.GetAll());
      return text; 
    }

    public static bool HasErrors (this IBufferingLog log) {
      var hasErrors = log.GetAll().Any(e => e.EntryType == LogEntryType.Error);
      return hasErrors;
    }

  }
}
