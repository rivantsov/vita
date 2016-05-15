using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Modules.Notifications {

  /// <summary>
  /// Test helper class. Blocks all outouing notifications (emails) and keeps them in internal list; 
  /// so unit test code can check/find sent messages. 
  /// </summary>
  public class NotificationListener  {
    List<NotificationMessage> _sentMessages = new List<NotificationMessage>(); 
    INotificationService _notificationService;
    Func<NotificationMessage, bool> _filter; 
    bool _blockAll;
    int _maxMessages;
    object _lock = new object();

    public NotificationListener(EntityApp app, Func<NotificationMessage, bool> filter = null, bool blockAll = false, int maxMessages = 100) {
      _blockAll = blockAll;
      _filter = filter; 
      _maxMessages = maxMessages; 
      _notificationService = app.GetService<INotificationService>();
      Util.Check(_notificationService != null, "Notification service is not registered.");
      _notificationService.Sending += NotificationService_Sending;
    }

    void NotificationService_Sending(object sender, NotificationEventArgs e) {
      if (_filter != null && !_filter(e.Message))
        return; 
      lock (_lock) {
        while (_sentMessages.Count > _maxMessages)
          _sentMessages.RemoveAt(0);
        _sentMessages.Add(e.Message);
        if (_blockAll)
          e.Message.Status = MessageStatus.Blocked; //to prevent it from sending
      }
    }

    // Methods for inspecting sent messages in tests
    public IList<NotificationMessage> GetMessages(int maxCount = 100) {
      lock(_lock)
        return _sentMessages.Reverse<NotificationMessage>().Take(maxCount).ToList();
    }
    public IList<NotificationMessage> GetMessagesTo(string recipient) {
      lock (_lock) {
        return _sentMessages.Where(m => m.Recipients == recipient).ToList();
      }
    }
    public NotificationMessage GetLastMessageTo(string recipient) {
      lock(_lock)
        return _sentMessages.LastOrDefault(m => m.Recipients == recipient); 
    }

  }
}
