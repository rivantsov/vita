using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {

  //Used for saving log entries through background save service
  internal class LoginLogEntry : LogEntry {
    public Guid? LoginId;
    public string EventType;
    public string Notes;
    public LoginLogEntry(OperationContext context, Guid? loginId, string eventType, string notes, string userName = null)  : base(context) {
      LoginId = loginId;
      EventType = eventType;
      Notes = notes;
      if(userName != null)
        base.UserName = userName;
    }
  }

}
