using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  // some information shared between multiple log entries; copied from OperationContext
  public class LogContext {
    public static LogContext SystemLogContext = new LogContext() { User = UserInfo.System };

    public UserInfo User;
    public Guid? SessionId;
    public Guid? WebCallId;
    public string Tenant; 

    public LogContext() { }

    public LogContext(OperationContext context) {
      User = context.User;
    }

  }

}
