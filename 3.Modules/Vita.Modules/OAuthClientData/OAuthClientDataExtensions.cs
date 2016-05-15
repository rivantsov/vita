using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClientData {

  public static class OAuthClientDataExtensions {
    public static IOAuthServer NewOAuthServer(this IEntitySession session, string name,
                                                string clientIdentifier, string clientSecret,
                                                string tempCredentialRequestUrl, string authorizationUrl, string tokenRequestUrl,
                                                string encryptionChannelName = null) {
      var srv = session.NewEntity<IOAuthServer>();
      srv.Name = name;
      srv.ClientIdentifier = session.NewOrUpdate(srv.ClientIdentifier, clientIdentifier, encryptionChannelName);
      srv.ClientSecret =  session.NewOrUpdate(srv.ClientSecret, clientSecret, encryptionChannelName);
      srv.TempCredentialRequestUrl = tempCredentialRequestUrl;
      srv.AuthorizationUrl = authorizationUrl;
      srv.TokenRequestUrl = tokenRequestUrl;
      return srv;
    }

    public static IOAuthTempCredentials NewOAuthTempCredentials(this IOAuthServer server, string token, string secret, DateTime expiresOn, Guid? userId) {
      var session = EntityHelper.GetSession(server); 
      var ent = session.NewEntity<IOAuthTempCredentials>();
      ent.Server = server;
      ent.UserId = userId;
      ent.TempToken = token;
      ent.TempSecret = secret;
      ent.ExpiresOn = expiresOn; 
      return ent; 
    }

    public static IOAuthCredentials NewOAuthCredentials(this IOAuthServer server, Guid userId, string token, string secret,
                        string remoteUserId, DateTime expiresOn, string encryptionChannelName = null) {
      var session = EntityHelper.GetSession(server);
      var ent = session.NewEntity<IOAuthCredentials>();
      ent.Server = server;
      ent.UserId = userId;
      ent.AuthorizationToken = session.NewOrUpdate(ent.AuthorizationToken, token, encryptionChannelName);
      ent.AuthorizationSecret = session.NewOrUpdate(ent.AuthorizationSecret, secret, encryptionChannelName);
      ent.RemoteUserId = remoteUserId; 
      ent.ExpiresOn = expiresOn; 
      return ent;
    }

  }//class

}
