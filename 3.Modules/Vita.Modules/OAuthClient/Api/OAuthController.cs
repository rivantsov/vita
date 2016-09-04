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
  [ApiRoutePrefix("oauth"), LoggedInOnly, Secured, ApiGroup("OAuthClient")]
  public class OAuthController: SlimApiController {

    protected IEntitySession OpenSession() {
      // we have async operations in this controller, which may take a lot of time. So we don't want to keep connections open
      this.Context.DbConnectionMode = DbConnectionReuseMode.NoReuse;
      return Context.OpenSecureSession();
    }

    /// <summary>Returns user status information with a remote OAuth server.</summary>
    /// <param name="serverName">Server name as registered in the system.</param>
    /// <returns>Status information object.</returns>
    [ApiGet, ApiRoute("{servername}/user/status")]
    public OAuthUserStatus GetUserOAuthStatus(string serverName) {
      var service = Context.App.GetService<IOAuthClientService>();
      var session = Context.OpenSession();
      var accessToken = service.GetUserOAuthToken(session, Context.User.UserId, serverName);
      return accessToken.ToOAuthStatus();
    }

    /// <summary>Revokes user permissions (authorization token) for a remote server. </summary>
    /// <param name="serverName">Server name as registered in the system.</param>
    /// <returns>None.</returns>
    /// <remarks>If user token is not found, or token is not active, the method does nothing.</remarks>
    [ApiGet, ApiRoute("{servername}/user/revoke")]
    public async Task RevokeUserPermissions(string serverName) {
      var service = Context.App.GetService<IOAuthClientService>();
      var session = Context.OpenSession();
      var token = service.GetUserOAuthToken(session, Context.User.UserId, serverName);
      if(token == null || token.Status != OAuthTokenStatus.Active)
        return; 
      await service.RevokeAccessTokenAsync(this.Context, token.Id);
    }

    /// <summary>Retrieves the client account (registered application) information for a given remote OAuth server.</summary>
    /// <param name="serverName">Remote server name.</param>
    /// <returns>Account information object.</returns>
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
      /// <summary>Space-separated list of scopes (types of information) to request authorization to access. </summary>
      public string Scopes { get; set; }
    }

    /// <summary>Starts OAuth authorization process (flow). </summary>
    /// <param name="serverName">The name of the server.</param>
    /// <param name="scopesParam">List of scopes.</param>
    /// <returns>Authorization flow object. </returns>
    /// <remarks>The AuthorizationUrl property of the returned object contains the URL pointing to target site authorization page.
    /// Open this page in a popup window or in a separate tab to allow user to authorize access on the target OAuth site.</remarks>
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

    /// <summary>Retrieves an authorization flow object representing an OAuth authorization process in progress. </summary>
    /// <param name="serverName">Server name.</param>
    /// <param name="id">Authorizaton flow (process) ID.</param>
    /// <returns>Authorization flow object.</returns>
    [ApiGet, ApiRoute("{servername}/flow/{id}")]
    public OAuthFlow GetOAuthFlow(string serverName, Guid id) {
      var session = OpenSession();
      var flow = session.GetEntity<IOAuthClientFlow>(id);
      return flow.ToModel(); 
    }

    /// <summary>Retrieves authorization token from the remote server using authorization code returned by the server in prior 
    /// redirect action.</summary>
    /// <param name="serverName">Server name.</param>
    /// <param name="id">Authorization flow ID.</param>
    /// <returns>User authorization status object.</returns>
    /// <remarks>This is a final action in OAuth authorization process. After user authorizes access on remote server, the server
    /// hits back the client app with redirect action, providing authorization code. The hit-back action is handled 
    /// by a separate OAuthRedirectController; the handler saves the authorization code in database and updates the flow status. 
    /// After that the application should invoke action to retrieve the authorization token (this method). 
    /// Once the call returns successfully, the client application has authorization token that it can use to access the user 
    /// information.</remarks>
    [ApiPost, ApiRoute("{servername}/flow/{id}/token")]
    public async Task<OAuthUserStatus> RetrieveAccessToken(string serverName, Guid id) {
      var service = Context.App.GetService<IOAuthClientService>();
      var tokenId = await service.RetrieveAccessTokenAsync(Context, id);
      return GetUserOAuthStatus(serverName);
    }

  }//class
}//ns
