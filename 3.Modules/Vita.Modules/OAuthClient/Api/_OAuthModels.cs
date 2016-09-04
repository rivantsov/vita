using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.OAuthClient.Api {
  /// <summary>Represents information about an application registered with a remote OAuth server. </summary>
  public class OAuthAccount {
    public Guid Id;
    public string Server;
    public string AccountName;
  }

  /// <summary>Contains information about user authorization status with remote OAuth server.</summary>
  /// <remarks>The access is authorized if the local system has an authorization token previously obtained 
  /// through OAuth 2.0 authorization process.</remarks>
  public class OAuthUserStatus {
    public Guid? UserId;
    public bool Authorized;
    public DateTime? RetrievedOn;
    public DateTime? ExpiresOn;
    public string[] Scopes;
  }

  /// <summary>Represents an authorization process for a user with a remote OAuth Server, completed or in progress.</summary>
  public class OAuthFlow {
    public Guid Id;
    public string Server;
    public Guid? UserId; 
    public OAuthFlowStatus Status;
    public string AuthorizationUrl;
  }

  public static class OAuthModelExtensions {

    public static OAuthUserStatus ToOAuthStatus(this IOAuthAccessToken accessToken) {
      if(accessToken == null)
        return new OAuthUserStatus() { Authorized = false };
      var session = EntityHelper.GetSession(accessToken);
      var utcNow = session.Context.App.TimeService.UtcNow;
      if(accessToken.ExpiresOn < utcNow)
        return new OAuthUserStatus() { Authorized = false };
      return new OAuthUserStatus() {
        Authorized = true, RetrievedOn = accessToken.RetrievedOn, ExpiresOn = accessToken.ExpiresOn,
        Scopes = accessToken.GetScopes(), UserId = accessToken.UserId
      };
    }

    public static OAuthAccount ToModel(this IOAuthRemoteServerAccount account) {
      if(account == null)
        return null;
      return new OAuthAccount() { Id = account.Id, AccountName = account.Name, Server = account.Server.Name };
    }

    public static OAuthFlow ToModel(this IOAuthClientFlow flow) {
      if(flow == null)
        return null;
      return new OAuthFlow() { Id = flow.Id, Status = flow.Status, Server = flow.Account.Server.Name,
        UserId = flow.UserId,  AuthorizationUrl = flow.AuthorizationUrl };
    }

  }//class

} //ns
