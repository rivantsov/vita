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

  }
}
