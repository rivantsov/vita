using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Modules.EncryptedData;
using Vita.Modules.OAuthClient.Internal;
using Vita.Modules.WebClient;

namespace Vita.Modules.OAuthClient {

  public static class OAuthClientExtensions {
    public static IOAuthRemoteServer NewOAuthRemoteServer(this IEntitySession session, string name, 
                                        OAuthServerOptions options, string siteUrl,                                               
                                        string authorizationUrl, string tokenRequestUrl, string tokenRefreshUrl, 
                                        string documentationUrl, string basicProfileUrl) {
      var srv = session.NewEntity<IOAuthRemoteServer>();
      srv.Name = name;
      srv.Options = options;
      srv.SiteUrl = siteUrl;  
      srv.AuthorizationUrl = authorizationUrl;
      srv.TokenRequestUrl = tokenRequestUrl;
      srv.TokenRefreshUrl = tokenRefreshUrl;
      srv.DocumentationUrl = documentationUrl;
      srv.BasicProfileUrl = basicProfileUrl; 
      return srv;
    }

    public static IOAuthRemoteServerAccount NewOAuthAccount(this IOAuthRemoteServer server, string name, Guid? ownerId,
                   string clientIdentifier, string clientSecret, string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(server);
      var acct = session.NewEntity<IOAuthRemoteServerAccount>();
      acct.Server = server;
      acct.Name = name;
      acct.ClientIdentifier = clientIdentifier;
      acct.ClientSecret = session.NewOrUpdate(acct.ClientSecret, clientSecret, encryptionChannelName);
      return acct; 
    }

    public static IOAuthClientFlow NewOAuthFlow(this IOAuthRemoteServerAccount account) {
      var session = EntityHelper.GetSession(account);
      var flow = session.NewEntity<IOAuthClientFlow>();
      flow.Account = account;
      flow.Status = OAuthClientProcessStatus.Started;
      return flow; 
    }

    public static IOAuthAccessToken NewOAuthAccessToken(this IOAuthRemoteServerAccount account, Guid? userId, 
                                                       string accessToken, string refreshToken, string openIdToken,
                                                       string scopes,
                                                       DateTime retrievedOn, DateTime expiresOn, 
                                                       string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(account);
      var ent = session.NewEntity<IOAuthAccessToken>();
      ent.Account = account;
      ent.UserId = userId;
      ent.AuthorizationToken = session.NewOrUpdate(ent.AuthorizationToken, accessToken, encryptionChannelName);
      if (!string.IsNullOrWhiteSpace(refreshToken))
        ent.RefreshToken = session.NewOrUpdate(ent.RefreshToken, refreshToken, encryptionChannelName);
      //if (!string.IsNullOrWhiteSpace(openIdToken))
      //  ent.OpenIdToken = <Unpack Open Id token> 
      ent.Scopes = scopes;
      ent.RetrievedOn = retrievedOn; 
      ent.ExpiresOn = expiresOn;
      return ent;
    }

    public static IOAuthOpenIdToken NewOpenIdToken(this IOAuthAccessToken accessToken, OpenIdToken idToken, string json) {
      var session = EntityHelper.GetSession(accessToken);
      var tknEnt = accessToken.OpenIdToken = session.NewEntity<IOAuthOpenIdToken>();
      tknEnt.Issuer = idToken.Issuer;
      tknEnt.AuthContextRef = idToken.ContextRef;
      tknEnt.Subject = idToken.Subject;
      tknEnt.Audience = idToken.Audience;
      tknEnt.IssuedAt = OpenIdConnectUtil.FromUnixTime(idToken.IssuedAt);
      tknEnt.ExpiresAt = OpenIdConnectUtil.FromUnixTime(idToken.ExpiresAt);
      if(idToken.AuthTime > 0)
        tknEnt.AuthTime = OpenIdConnectUtil.FromUnixTime(idToken.AuthTime);
      tknEnt.FullJson = json; 
      return tknEnt; 
    }

    public static bool IsSet(this OAuthServerOptions options, OAuthServerOptions option) {
      return (options & option) != 0;
    }

  }//class

}
