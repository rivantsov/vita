using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Modules.EncryptedData;
using Vita.Modules.WebClient;

namespace Vita.Modules.OAuthClient {

  public static class OAuthClientExtensions {
    public static IOAuthRemoteServer NewOAuthRemoteServer(this IEntitySession session, string name, 
                OAuthServerOptions options, string siteUrl,                                               
                string authorizationUrl, string tokenRequestUrl, string tokenRefreshUrl, 
                string documentationUrl, string basicProfileUrl, string profileUserIdTag, 
                string tokenRevokeUrl = null) {
      var srv = session.NewEntity<IOAuthRemoteServer>();
      srv.Name = name;
      srv.Options = options;
      srv.SiteUrl = siteUrl;  
      srv.AuthorizationUrl = authorizationUrl;
      srv.TokenRequestUrl = tokenRequestUrl;
      srv.TokenRefreshUrl = tokenRefreshUrl;
      srv.DocumentationUrl = documentationUrl;
      srv.BasicProfileUrl = basicProfileUrl;
      srv.ProfileUserIdTag = profileUserIdTag;
      srv.TokenRevokeUrl = tokenRevokeUrl;
      return srv;
    }

    public static IOAuthRemoteServerAccount NewOAuthAccount(this IOAuthRemoteServer server, 
                       string clientIdentifier, string clientSecret,
                       string accountName, Guid? ownerId = null, string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(server);
      var acct = session.NewEntity<IOAuthRemoteServerAccount>();
      acct.Server = server;
      acct.Name = accountName;
      acct.ClientIdentifier = clientIdentifier;
      acct.ClientSecret = session.NewOrUpdate(acct.ClientSecret, clientSecret, encryptionChannelName);
      return acct; 
    }

    public static IOAuthClientFlow NewOAuthFlow(this IOAuthRemoteServerAccount account) {
      var session = EntityHelper.GetSession(account);
      var flow = session.NewEntity<IOAuthClientFlow>();
      flow.Account = account;
      flow.Status = OAuthFlowStatus.Started;
      return flow; 
    }

    public static IOAuthAccessToken NewOAuthAccessToken(this IOAuthRemoteServerAccount account, Guid? userId, 
                     string accessToken, OAuthTokenType tokenType, string refreshToken, string openIdToken,
                     string scopes, DateTime retrievedOn, DateTime expiresOn, 
                     string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(account);
      var ent = session.NewEntity<IOAuthAccessToken>();
      ent.Account = account;
      ent.UserId = userId;
      ent.AccessToken = session.NewOrUpdate(ent.AccessToken, accessToken, encryptionChannelName);
      ent.TokenType = tokenType;
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

    public static IOAuthExternalUser NewExternalUser(this IOAuthRemoteServer server, Guid userId, string externalUserId) {
      var session = EntityHelper.GetSession(server);
      var user = session.NewEntity<IOAuthExternalUser>();
      user.Server = server;
      user.UserId = userId;
      user.ExternalUserId = externalUserId;
      return user; 
    }

    public static IOAuthRemoteServer GetOAuthServer(this IEntitySession session, string serverName) {
      return session.EntitySet<IOAuthRemoteServer>().Where(s => s.Name == serverName).FirstOrDefault();
    }

    public static string[] GetScopes(this IOAuthRemoteServer server) {
      if(string.IsNullOrWhiteSpace(server.Scopes))
        return new string[] { }; 
      return server.Scopes.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static string[] GetScopes(this IOAuthAccessToken token) {
      if(string.IsNullOrWhiteSpace(token.Scopes))
        return new string[] { };
      return token.Scopes.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static IOAuthRemoteServerAccount GetOAuthAccount(this IEntitySession session, string serverName, string accountName = null) {
      if(accountName == null) {
        var stt = session.GetOAuthSettings(); 
        accountName = stt.DefaultAccountName;
      }
      var accountQuery = session.EntitySet<IOAuthRemoteServerAccount>().Where(a => a.Server.Name == serverName && a.Name == accountName);
      var act = accountQuery.FirstOrDefault();
      return act;
    }

    public static IOAuthClientFlow BeginOAuthFlow(this IOAuthRemoteServerAccount account, Guid? userId = null, string scopes = null) {
      var session = EntityHelper.GetSession(account);
      var stt = session.GetOAuthSettings(); 
      var flow = account.NewOAuthFlow();
      var redirectUrl = stt.RedirectUrl;
      if(account.Server.Options.IsSet(OAuthServerOptions.TokenReplaceLocalIpWithLocalHost))
        redirectUrl = redirectUrl.Replace("127.0.0.1", "localhost"); 
      flow.UserId = userId;
      flow.Scopes = scopes ?? account.Server.Scopes; //all scopes
      flow.RedirectUrl = redirectUrl;
      var clientId = account.ClientIdentifier;
      flow.AuthorizationUrl = account.Server.AuthorizationUrl + 
            StringHelper.FormatUri(OAuthClientModule.AuthorizationUrlQuery, clientId, redirectUrl,
            flow.Scopes, flow.Id.ToString());
      return flow;
    }

    private static OAuthClientSettings GetOAuthSettings(this IEntitySession session) {
      var stt = session.Context.App.GetConfig<OAuthClientSettings>();
      Util.Check(stt != null, "OAuthClientSettings object is not registered in the entity app. Most likely OAuthClientModule is not part of the app.");
      return stt; 
    }

    public static IOAuthExternalUser FindUser(this IOAuthRemoteServer server, string externalUserId) {
      var session = EntityHelper.GetSession(server);
      var user = session.EntitySet<IOAuthExternalUser>().Where(u => u.Server == server && u.ExternalUserId == externalUserId).FirstOrDefault();
      return user; 
    }

    // A primitive way of finding user id inside json, by finding property by name (specified in IOAuthRemoteServer) and extracting its value,
    // without converting Json into strongly typed object
    public static string ExtractUserId(this IOAuthRemoteServer server, string profileJson) {
      if(string.IsNullOrWhiteSpace(server.ProfileUserIdTag))
        return null;
      var qtag = '"' + server.ProfileUserIdTag + '"';
      var tagPos = profileJson.IndexOf(qtag);
      if(tagPos < 0)
        return null;
      var start = tagPos + qtag.Length + 1;
      var qLeft = profileJson.IndexOf('"', start);
      var qRight = profileJson.IndexOf('"', qLeft + 1);
      var userId = profileJson.Substring(qLeft + 1, qRight - qLeft - 1);
      return userId;
    }

    internal static void UpdateStatus(this IEntitySession session, Guid tokenId, OAuthTokenStatus status) {
      var updQuery = from t in session.EntitySet<IOAuthAccessToken>()
                     where t.Id == tokenId
                     select new { Id = t.Id, Status = status };
      var cnt = updQuery.ExecuteUpdate<IOAuthAccessToken>();
    }

    public static bool IsSet(this OAuthServerOptions options, OAuthServerOptions option) {
      return (options & option) != 0;
    }

  }//class

}
