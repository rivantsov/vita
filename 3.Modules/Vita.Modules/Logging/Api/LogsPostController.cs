using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Web;

namespace Vita.Modules.Logging.Api {

  // Publicly open constroller for posting events, useful for recording events
  // (clicks, browse) for not logged in users. 
  // Must be registed explicitly in AppConfig; BookStore sample app does this
  [ApiRoutePrefix("logs-post"), ApiGroup("Logs-Post")]
  public class LogsPostController : SlimApiController {
    public static bool EnablePublicEvents = false;

    [ApiPost, ApiRoute("clienterrors")]
    public Guid PostClientError(ClientError error) {
      Context.ThrowIf(!EnablePublicEvents, ClientFaultCodes.InvalidAction, "EnablePublicEvents",
        "Event info cannot be posted if user is not logged in. LogPostController.EnablePublicEvents is false.");
      Context.ThrowIfNull(error, ClientFaultCodes.ContentMissing, "error", "Error object is missing.");
      var errLog = Context.App.GetService<IErrorLogService>();
      Context.ThrowIfNull(errLog, ClientFaultCodes.ObjectNotFound, "ErrorLog", "Error log service not configured on server.");
      var errId = errLog.LogClientError(this.Context, error.Id, error.Message, error.Details, error.AppName, error.LocalTime);
      return errId;
    }

    [ApiPost, ApiRoute("events/public")]
    public void PostEventsPublic(IList<EventData> events) {
      Context.ThrowIf(!EnablePublicEvents, ClientFaultCodes.InvalidAction, "EnablePublicEvents",
        "Event info cannot be posted if user is not logged in. LogPostController.EnablePublicEvents is false.");
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
