using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services.Implementations;
using Vita.Modules.Smtp;
using Vita.Modules.Logging;

namespace Vita.Modules.Notifications {
  public class NotificationService : INotificationService, IEntityService {
    EntityApp _app;
    INotificationLogService _log; 

    public NotificationService(EntityApp app) {
      _app = app;
      _app.RegisterService<INotificationService>(this); 
    }

    public static INotificationService Create(EntityApp app) {
      var instance = new NotificationService(app);
      instance.Init(app);
      return instance; 
    }

    #region IEntityService
    public void Init(Entities.EntityApp app) {
      _log = app.GetService<INotificationLogService>(); 
      //init providers
      foreach (var prov in _providers)
        prov.Init(app); 
    }

    public void Shutdown() { 

    }

    #endregion

    #region INotificationService members
    public IList<INotificationProvider> Providers {
      get { return _providers;  }
    }  IList<INotificationProvider> _providers = new List<INotificationProvider>();

    public void Send(OperationContext context, NotificationMessage message) {
      AsyncHelper.RunSync(() => SendAsync(context, message));
    }

    public async Task SendAsync(OperationContext context, NotificationMessage message) {
      message.AttemptCount++;
      var provider = _providers.FirstOrDefault(c => c.CanSend(message));
      var args = new NotificationEventArgs(message, provider);
      if (Sending != null) {
        Sending(this, args);
        if (message.Status == MessageStatus.Blocked || message.Status == MessageStatus.Sent) {
          if (_log != null)
            _log.LogMessage(context, message);
          return;
        }
        provider = args.Provider; 
      }
      Util.Check(provider != null, 
        "Notification service failed to find notification provider for a message, message type: '{0}', media type: '{1}'.", 
        message.Type, message.MediaType);
      await provider.SendAsync(context, message).ConfigureAwait(false); 
      if (Sent != null)
        Sent(this, args); 
      if (_log != null)
        _log.LogMessage(context, message);
    }

    public event EventHandler<NotificationEventArgs> Sending;

    public event EventHandler<NotificationEventArgs> Sent;
    #endregion 
  }//class
}//ns
