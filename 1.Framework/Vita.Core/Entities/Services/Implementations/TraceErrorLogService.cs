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
      return Guid.Empty;
    }
    public Guid LogError(string message, string details, OperationContext context) {
      Util.WriteToTrace(message, details, context.GetLogContents());
      return Guid.Empty;
    }
    public Guid LogClientError(OperationContext context, Guid? id, string message, string details, string appName, DateTime? occurredOn = null) {
      Util.WriteToTrace("ClientError: " + message, details, null);
      return Guid.Empty; 
    }

    #endregion
  }

}
