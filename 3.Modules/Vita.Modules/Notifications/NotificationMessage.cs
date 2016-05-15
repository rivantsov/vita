using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Notifications {

  public enum MessageStatus {
    Created,
    Sending,
    Sent,
    Blocked, //custom code intercepted and sent it using some other channel, or just canceled the message
    Error, 
    Failed, //failed completely, after n repeats
  }

  public class NotificationMessage {
    public MessageStatus Status;
    public string Recipients;
    public string From; 
    public Guid? MainRecipientUserId;
    public string Type;
    public string MediaType; //preferred channel
    public int AttemptCount;
    public string Error; 

    public string Culture;
    public IDictionary<string, object> Parameters = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
    public IDictionary<string, object> Attachments = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
  }

}
