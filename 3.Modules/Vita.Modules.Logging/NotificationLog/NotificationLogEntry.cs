using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 
using Vita.Entities.Logging;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {
  // Temp object used for sending log informaton to background save service
  public class NotificationLogEntry : LogEntry {
    public NotificationMessage Message;

    public NotificationLogEntry(OperationContext context, NotificationMessage message)    : base(context) {
      base.Id = Guid.NewGuid(); 
      Message = message;
    }

  }
}
