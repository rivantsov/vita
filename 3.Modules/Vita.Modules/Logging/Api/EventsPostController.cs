using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Web;

namespace Vita.Modules.Logging.Api {

  //Publicly open constroller for posting events, does not require authentication; useful for recording events
  // (clicks, browse) for not logged in users. 
  // Must be registed explicitly in AppConfig; BookStore sample app does this
  public class EventsPostController : SlimApiController {

    [ApiPost, ApiRoute("events/public")]
    public void PostEventsPublic(IList<EventData> events) {
      var eventLog = Context.App.GetService<IEventLogService>();
      eventLog.LogEvents(events); 
    }

    [ApiPost, ApiRoute("events"), LoggedInOnly]
    public void PostEvents(IList<EventData> events) {
      var eventLog = Context.App.GetService<IEventLogService>();
      //Fill-out missing fields
      foreach (var evt in events) {
        evt.UserId = evt.UserId ?? Context.User.UserId;
        evt.SessionId = evt.SessionId ?? Context.UserSession.SessionId;
        evt.StartedOn = evt.StartedOn ?? Context.App.TimeService.UtcNow;
        evt.Location = evt.Location ?? Context.WebContext.IPAddress;
      }
      eventLog.LogEvents(events);
    }


  }
}
