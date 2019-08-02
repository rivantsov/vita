using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Logging {

  public static class AppEventLogExtensions {

    public static IAppEvent NewEvent(this IEntitySession session, EventData data) {
      var ev = session.NewEntity<IAppEvent>();
      ev.Id = data.Id;
      ev.EventType = data.EventType;
      ev.StartedOn = data.StartedOn ?? session.Context.App.TimeService.UtcNow;
      ev.Duration = data.Duration;
      ev.Location = data.Location;
      ev.UserId = data.UserId;
      ev.SessionId = data.SessionId;
      ev.TenantId = data.TenantId;
      ev.Value = data.Value;
      ev.StringValue = data.StringValue ?? data.Value + string.Empty; 
      if (data.Parameters != null && data.Parameters.Count > 0)
        foreach (var de in data.Parameters) {
          var prm = session.NewEntity<IAppEventParameter>();
          prm.Event = ev;
          prm.Name = de.Key;
          prm.Value = de.Value;
        }
      return ev; 
    }//method

  }//class
}
