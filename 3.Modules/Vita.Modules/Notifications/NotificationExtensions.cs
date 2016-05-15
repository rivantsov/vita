using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Notifications {
  public static class NotificationExtensions {

    public static string GetString(this NotificationMessage message, string contentKey) {
      object value; ;
      if (message.Parameters.TryGetValue(contentKey, out value))
        return (string)value;
      return null; 
    }

  }
}
