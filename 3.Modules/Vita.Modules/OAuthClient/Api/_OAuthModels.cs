using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.OAuthClient.Api {

  public class OAuthAccount {
    public Guid Id;
    public string Server;
    public string AccountName;
  }

  public class OAuthUserStatus {
    public Guid? UserId;
    public bool Authorized;
    public DateTime? RetrievedOn;
    public DateTime? ExpiresOn;
    public string[] Scopes;
  }

  public class OAuthFlow {
    public Guid Id;
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
      return new OAuthFlow() { Id = flow.Id, Status = flow.Status, AuthorizationUrl = flow.AuthorizationUrl };
    }

  }//class

} //ns
