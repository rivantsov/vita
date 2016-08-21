using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 
using Vita.Entities.Web;
using Vita.Modules.OAuthClient;

namespace Vita.Modules.OAuthClient.Api {

  /// <summary>Secured controller for OAuth client flow to obtain consent for a logged in user.</summary>
  [ApiRoutePrefix("oauth"), LoggedInOnly, Secured, ApiGroup("OAuth")]
  public class OAuthController: SlimApiController {

    protected IEntitySession OpenSession() {
      // we have async operations in this controller, which may take a lot of time. So we don't want to keep connections open
      this.Context.DbConnectionMode = DbConnectionReuseMode.NoReuse;
      return Context.OpenSecureSession();
    }

    [ApiGet, ApiRoute("{servername}/user/status")]
    public OAuthUserStatus GetUserOAuthStatus(string serverName) {
      var service = Context.App.GetService<IOAuthClientService>();
      var session = Context.OpenSession();
      var accessToken = service.GetUserOAuthToken(session, Context.User.UserId, serverName);
      return accessToken.ToOAuthStatus();
    }


    [ApiGet, ApiRoute("{servername}/account")]
    public OAuthAccount GetAccount(string serverName) {
      var session = OpenSession();
      var account = session.GetOAuthAccount(serverName);
      if(account == null)
        return null;
      return account.ToModel();
    }

    // container for optional Scopes parameter in URL
    public class ScopesParam {
      public string Scopes { get; set; }
    }

    [ApiPost, ApiRoute("{servername}/flow")]
    public OAuthFlow BeginOAuthFlow(string serverName, [FromUrl] ScopesParam scopesParam) {
      var userId = Context.User.UserId;
      var session = OpenSession();
      using(session.WithElevateRead()) {
        var acct = session.GetOAuthAccount(serverName);
        Context.ThrowIfNull(acct, ClientFaultCodes.ObjectNotFound, "serverName", "Account not registered for server {0}.", serverName);
        var service = Context.App.GetService<IOAuthClientService>();
        var scopes = scopesParam.Scopes ?? acct.Server.Scopes; //take all scopes
        var flow = acct.BeginOAuthFlow(Context.User.UserId, scopes);
        session.SaveChanges();
        return flow.ToModel();
      }
    }

    [ApiGet, ApiRoute("{servername}/flow/{id}")]
    public OAuthFlow GetOAuthFlow(string serverName, Guid id) {
      var session = OpenSession();
      var flow = session.GetEntity<IOAuthClientFlow>(id);
      return flow.ToModel(); 
    }

    [ApiPost, ApiRoute("{servername}/flow/{id}/token")]
    public async Task<OAuthUserStatus> RetrieveAccessToken(string serverName, Guid id) {
      var service = Context.App.GetService<IOAuthClientService>();
      var tokenId = await service.RetrieveAccessToken(Context, id);
      return GetUserOAuthStatus(serverName);
    }

  }//class
}//ns
