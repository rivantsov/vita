using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;
using Vita.Modules.OAuthClient.Internal;

namespace Vita.Modules.OAuthClient {

  public static class OAuthClientDataExtensions {
    public static IOAuthRemoteServer NewOAuthRemoteServer(this IEntitySession session, string name, OAuthServerType serverType,
                                                
                                                string authorizationUrl, string tokenRequestUrl,
                                                string tokenRefreshUrl) {
      var srv = session.NewEntity<IOAuthRemoteServer>();
      srv.Name = name;
      srv.ServerType = serverType; 
      srv.AuthorizationUrl = authorizationUrl;
      srv.TokenRequestUrl = tokenRequestUrl;
      srv.TokenRefreshUrl = tokenRefreshUrl; 
      return srv;
    }

    public static IOAuthRemoteServerAccount NewOAuthAccount(this IOAuthRemoteServer server, string name, Guid? ownerId,
                   string clientIdentifier, string clientSecret, string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(server);
      var acct = session.NewEntity<IOAuthRemoteServerAccount>();
      acct.Server = server;
      acct.Name = name;
      acct.ClientIdentifier = session.NewOrUpdate(acct.ClientIdentifier, clientIdentifier, encryptionChannelName);
      acct.ClientSecret = session.NewOrUpdate(acct.ClientSecret, clientSecret, encryptionChannelName);
      return acct; 
    }
    public static IOAuthClientFlow NewOAuthFlow(this IOAuthRemoteServerAccount account) {
      var session = EntityHelper.GetSession(account);
      var flow = session.NewEntity<IOAuthClientFlow>();
      flow.Account = account;
      return flow; 
    }
    public static IOAuthRemoteServerAccessToken NewOAuthAccessToken(this IOAuthRemoteServerAccount account, Guid? userId, 
         string token, string refreshToken, DateTime expiresOn, string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(account);
      var ent = session.NewEntity<IOAuthRemoteServerAccessToken>();
      ent.Account = account;
      ent.UserId = userId;
      ent.AuthorizationToken = session.NewOrUpdate(ent.AuthorizationToken, token, encryptionChannelName);
      ent.RefreshToken = session.NewOrUpdate(ent.RefreshToken, refreshToken, encryptionChannelName);
      ent.ExpiresOn = expiresOn; 
      return ent;
    }

    public static IOAuthOpenIdToken NewOpenIdToken(this IOAuthRemoteServerAccessToken accessToken, OpenIdToken idToken) {
      var session = EntityHelper.GetSession(accessToken);
      var tkn = accessToken.OpenIdToken = session.NewEntity<IOAuthOpenIdToken>();
      tkn.Subject = idToken.Subject;
      return tkn; 
    }

  }//class

}
