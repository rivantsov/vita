using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.Notifications;

namespace Vita.Modules.Logging {
  //To be completed - add generic log method without use of MailMessage
  public interface INotificationLogService {
    Guid LogMessage(OperationContext context, NotificationMessage message);
  }
}
