using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services;
using Vita.Entities.Web;

namespace Vita.Modules.Logging.Api {
  
  // Controller getting information about current user session, refreshing auth token, setting local time offset
  [ApiRoutePrefix("usersession"), LoggedInOnly, ApiGroup("UserSession")]
  public class UserSessionInfoController : SlimApiController {

    /// <summary>Returns user session information. </summary>
    /// <returns>User session info object.</returns>
    [ApiGet, ApiRoute("")]
    public UserSessionInfo GetSessionInfo () {
      var session = Context.UserSession;
      return new UserSessionInfo() { Started = session.StartedOn, UserId = session.User.UserId, UserName = session.User.UserName,
           TimeOffsetMinutes = session.TimeZoneOffsetMinutes, Expires =  session.ExpirationEstimate };
    }

    /// <summary>Refreshes the user session token (authentication token). </summary>
    /// <returns>New authentication token.</returns>
    /// <remarks>For long running sessions (weeks/months), the client should regularly refresh authentication token for security reasons.
    /// Typeical use - mobile applications (phone).</remarks>
    [ApiPut, ApiRoute("token")]
    public BoxedValue<string> RefreshSessionToken() {
      var session = Context.UserSession;
      var serv = Context.App.GetService<IUserSessionService>();
      var token = serv.RefreshSessionToken(this.Context);
      return new BoxedValue<string>(token);
    }

    [ApiPut, ApiRoute("client-timezone-offset")]
    public void SetTimeZoneOffset(int minutes) {
      // user session will be marked as modified and saved
      Context.UserSession.TimeZoneOffsetMinutes = minutes; 
    }

  }

}
