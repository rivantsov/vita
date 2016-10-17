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
    /// <param name="req">Refresh token.</param>
    /// <returns>An object with new authentication token and new refresh token.</returns>
    /// <remarks>For long running sessions (weeks/months), the client should regularly refresh authentication token for security reasons.
    /// Typeical use - mobile applications (phone). Upon refresh the current session expiration is extended to new long session expiration period in the future.</remarks>
    [ApiPut, ApiRoute("token")]
    public RefreshResponse RefreshSessionToken(RefreshRequest req) {
      var session = Context.UserSession;
      var serv = Context.App.GetService<IUserSessionService>();
      var newRefreshToken = serv.RefreshSessionToken(this.Context, req.RefreshToken);
      return new RefreshResponse() { NewSessionToken = Context.UserSession.Token, NewRefreshToken = newRefreshToken };
    }

    [ApiPut, ApiRoute("client-timezone-offset")]
    public void SetTimeZoneOffset(int minutes) {
      // user session will be marked as modified and saved
      Context.UserSession.TimeZoneOffsetMinutes = minutes; 
    }

  }

}
