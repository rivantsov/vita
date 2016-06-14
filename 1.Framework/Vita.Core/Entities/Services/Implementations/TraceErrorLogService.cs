using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Entities.Services.Implementations {

  /// <summary>Default implementation of error log service - saves errors to Trace and Windows Event Log. </summary>
  public class TraceErrorLogService : IErrorLogService {

    #region IErrorLogService Members

    public Guid LogError(Exception exception, OperationContext context = null) {
      Util.WriteToTrace(exception, context.GetLogContents());
      OnErrorLogged(context, exception);
      return Guid.Empty;
    }
    public Guid LogError(string message, string details, OperationContext context) {
      Util.WriteToTrace(message, details, context.GetLogContents());
      OnErrorLogged(context, new Exception(message + Environment.NewLine + details));
      return Guid.Empty;
    }
    public Guid LogClientError(OperationContext context, Guid? id, string message, string details, string appName, DateTime? occurredOn = null) {
      Util.WriteToTrace("ClientError: " + message, details, null);
      OnErrorLogged(context, new Exception("ClientError: " + message + Environment.NewLine + details));
      return Guid.Empty; 
    }

    public event EventHandler<ErrorLogEventArgs> ErrorLogged; 

    private void OnErrorLogged(OperationContext context, Exception ex) {
      var evt = ErrorLogged;
      if(evt != null)
        evt(this, new ErrorLogEventArgs(context, ex)); 
    }
    #endregion
  }

}
