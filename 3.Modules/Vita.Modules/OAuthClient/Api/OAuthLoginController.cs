using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 
using Vita.Entities; 
using Vita.Entities.Web;
using Vita.Modules.Login;
using Vita.Modules.Login.Api;

namespace Vita.Modules.OAuthClient.Api {
  /// <summary>Unsecured controller for login using OAuth protocol with remote OAuth server.</summary>
  [ApiRoutePrefix("oauthlogin")]
  public class OAuthLoginController: SlimApiController {

    protected IEntitySession OpenSession() {
      // we have async operations in this controller, which may take a lot of time. So we don't want to keep connections open
      this.Context.DbConnectionMode = DbConnectionReuseMode.NoReuse;
      // we do not have logged in user, so we use system session (no authorization checks)
      return Context.OpenSystemSession();
    }

    [ApiPost, ApiRoute("{servername}/flow")]
    public OAuthFlow BeginOAuthFlow(string serverName, string scope) {
      var userId = Context.User.UserId;
      var session = OpenSession();
      var acct = session.GetOAuthAccount(serverName);
      Context.ThrowIfNull(acct, ClientFaultCodes.ObjectNotFound, "serverName", "Account not registered for server {0}.", serverName);
      var service = Context.App.GetService<IOAuthClientService>();
      var flow = acct.BeginOAuthFlow(Context.User.UserId, scope);
      session.SaveChanges();
      return flow.ToModel();
    }

    [ApiGet, ApiRoute("{servername}/flow/{id}")]
    public OAuthFlow GetOAuthFlow(string serverName, Guid id) {
      var session = OpenSession();
      var flow = session.GetEntity<IOAuthClientFlow>(id);
      return flow.ToModel(); 
    }

    [ApiPost, ApiRoute("{servername}/flow/{flowid}/login")]
    public async Task<LoginResponse> Login(string serverName, Guid flowId) {
      var session = OpenSession();
      var service = Context.App.GetService<IOAuthClientService>();
      var loginService = Context.App.GetService<ILoginService>();
      Util.Check(loginService != null, "Login service not found, cannot login.");
      var server = session.GetOAuthServer(serverName);
      Util.Check(server != null, "Server {0} not registered.", server.Name);
      var tokenId = await service.RetrieveAccessToken(Context, flowId);
      var token = session.GetEntity<IOAuthAccessToken>(tokenId);
      var oauthStatus = token.ToOAuthStatus(); 
      if (!oauthStatus.Authorized)
        return new LoginResponse() { Status = LoginAttemptStatus.Failed };
      //get basic profile
      var profile = await service.GetBasicProfile(this.Context, tokenId);
      var extUserId = server.ExtractUserId(profile);
      Util.CheckNotEmpty(extUserId, "Failed to extract user ID from profile Json. Profile: {0}", profile);
      var extUser = server.FindUser(extUserId); 
      if (extUser == null)
        return new LoginResponse() { Status = LoginAttemptStatus.Failed };
      var userId = extUser.UserId;
      var loginResult = loginService.LoginUser(this.Context, userId);
      if (loginResult.Status != LoginAttemptStatus.Success)
        return new LoginResponse() { Status = LoginAttemptStatus.Failed };
      var displayName = Context.App.GetUserDispalyName(loginResult.User);
      return new LoginResponse() {
        Status = LoginAttemptStatus.Success, UserId = userId, UserName = loginResult.User.UserName,
        AuthenticationToken = loginResult.SessionToken, UserDisplayName = displayName, LoginId = loginResult.Login.Id, LastLoggedInOn = loginResult.LastLoggedInOn
      };
    }

  }//class
}//ns
