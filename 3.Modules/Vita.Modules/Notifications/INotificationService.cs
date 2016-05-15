using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services.Implementations;

namespace Vita.Modules.Notifications {

  public class NotificationEventArgs : EventArgs {
    public readonly NotificationMessage Message;
    public INotificationProvider Provider;
    public NotificationEventArgs(NotificationMessage message, INotificationProvider provider) {
      Message = message;
      Provider = provider; 
    }
  }

  public interface INotificationProvider {
    void Init(EntityApp app);
    bool CanSend(NotificationMessage message); 
    Task SendAsync(OperationContext context, NotificationMessage message);
  }

  public interface INotificationService {
    IList<INotificationProvider> Providers { get; }
    void Send(OperationContext context, NotificationMessage message);
    Task SendAsync(OperationContext context, NotificationMessage message);
    event EventHandler<NotificationEventArgs> Sending;
    event EventHandler<NotificationEventArgs> Sent;
  }

}
