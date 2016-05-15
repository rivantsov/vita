using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public class LogEntry {
    public Guid? Id;
    public DateTime CreatedOn;
    public string UserName;
    public Guid? UserSessionId;
    public Guid? WebCallId;
    public LogEntry(OperationContext context, DateTime? createdOn = null) {
      if (context != null) {
        CreatedOn = createdOn == null ? context.App.TimeService.UtcNow : createdOn.Value;
        UserName = context.User.UserName;
        if (context.UserSession != null)
          UserSessionId = context.UserSession.SessionId;
        if (context.WebContext != null)
          WebCallId = context.WebContext.Id; 
      }
    }
  }


}
