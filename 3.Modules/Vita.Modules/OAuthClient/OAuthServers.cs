using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.OAuthClient {

  public static class OAuthServers {

    public static void CreateUpdatePopularServers(IEntitySession session) {
      // Windows Live
      CreateOrUpdateServer(session, "WindowsLive",
        OAuthServerOptions.TokenReplaceLocalIpWithLocalHost,
        "https://www.live.com/",
        "https://login.live.com/oauth20_authorize.srf",
        "https://login.live.com/oauth20_token.srf",
        "https://login.live.com/oauth20_token.srf",  // refresh URL, same as access token URL
        "wl.basic wl.emails wl.photos wl.offline_access wl.signin",
        "https://msdn.microsoft.com/en-us/library/hh243647.aspx",
        "https://apis.live.net/v5.0/me", 
        "id");

      // Google 
      // Specifics: Refresh token is returned only in the first request for access token
      CreateOrUpdateServer(session, "Google",
          OAuthServerOptions.OpenIdConnect,
          "http://www.google.com",
          "https://accounts.google.com/o/oauth2/v2/auth",
          "https://www.googleapis.com/oauth2/v4/token",
          "https://www.googleapis.com/oauth2/v4/token",
          "profile email",
          "https://developers.google.com/identity/protocols/OAuth2WebServer",
          "https://www.googleapis.com/plus/v1/people/me", 
          "id");

      // Facebook 
      // TODO: Investigage; looks like FB supports id_token (like in OpenIdConnect), but requires some twists 
      // investigate why currently does not return id_token
      CreateOrUpdateServer(session, "Facebook",
        OAuthServerOptions.TokenUseGet | OAuthServerOptions.OpenIdConnect | OAuthServerOptions.TokenReplaceLocalIpWithLocalHost,
        "http://www.facebook.com",
        "https://www.facebook.com/dialog/oauth",
        "https://graph.facebook.com/v2.3/oauth/access_token",
        null,
        "public_profile email",
        "https://developers.facebook.com/docs/facebook-login/manually-build-a-login-flow",
        "https://graph.facebook.com/v2.5/me", 
        "id");

      // LinkedIn. Specifics: 
      //   1. LinkedIn uses Get for access token endpoint (OAuth2 spec requires POST)
      CreateOrUpdateServer(session, "LinkedIn", 
        OAuthServerOptions.TokenUseGet,
        "http://www.linkedin.com",
        "https://www.linkedin.com/oauth/v2/authorization",
        "https://www.linkedin.com/oauth/v2/accessToken",
        null,  
        "r_basicprofile r_emailaddress rw_company_admin w_share",
        "https://developer.linkedin.com/docs/oauth2",
        "https://api.linkedin.com/v1/people/~?format=json", 
        "id");

      // Fitbit. 
      //  1. Access token endpoint requries authorization header which is Base64 encoded 'clientid:clientsecret'
      CreateOrUpdateServer(session, "Fitbit", 
          OAuthServerOptions.RequestTokenClientInfoInAuthHeader,
          "https://www.fitbit.com",
          "https://www.fitbit.com/oauth2/authorize",
          "https://api.fitbit.com/oauth2/token",
          "https://api.fitbit.com/oauth2/token",  // refresh token
          "activity heartrate location nutrition profile settings sleep social weight",
          "https://dev.fitbit.com/docs/oauth2/",
          "https://api.fitbit.com/1/user/-/profile.json", 
          "encodedId");
        
        // Jawbone
        CreateOrUpdateServer(session, "Jawbone",
          OAuthServerOptions.TokenUseGet,
          "https://jawbone.com/",
          "https://jawbone.com/auth/oauth2/auth",
          "https://jawbone.com/auth/oauth2/token",
          "https://jawbone.com/auth/oauth2/token",  
          "basic_read extended_read location_read friends_read mood_read mood_write move_read move_write " +
            "sleep_read sleep_write meal_read meal_write weight_read weight_write " +
            "generic_event_read generic_event_write heartrate_read",
          "https://jawbone.com/up/developer/authentication",
          "https://jawbone.com/nudge/api/v.1.1/users/@me", 
          "user_xid");

      // Yahoo. Specifics: it is impossible to test - it does not allow localhost as redirect URL.
      // There are fancy hacks/workarounds (creating localtest.me in hosts file) - but that's too much.
      // It is disabled here, but you can enable it if you need Yahoo
      /*
      CreateOrUpdateServer(session, "Yahoo",
        OAuthServerOptions.OpenIdConnect | OAuthServerOptions.RequestTokenClientInfoInAuthHeader 
             | OAuthServerOptions.TokenReplaceLocalIpWithLocalHost,
        "http://www.yahoo.com",
        "https://api.login.yahoo.com/oauth2/request_auth",
        "https://api.login.yahoo.com/oauth2/get_token",
        "https://api.login.yahoo.com/oauth2/get_token",
        "sdps-r mail-r",
        "https://developer.yahoo.com/oauth2/guide/",
        "https://social.yahooapis.com/v1/user/abcdef123/profile?format=json");
      */


      session.SaveChanges();

    }

    public static IOAuthRemoteServer CreateOrUpdateServer(IEntitySession session,  string name, OAuthServerOptions options, 
                    string siteUrl, string authorizationUrl, string tokenRequestUrl, string tokenRefreshUrl, string scopes,
                    string documentationUrl, string basicProfileUrl, string profileUserIdTag) {
      IOAuthRemoteServer srv = session.EntitySet<IOAuthRemoteServer>().Where(s => s.Name == name).FirstOrDefault();
      if(srv == null)
        srv = session.NewEntity<IOAuthRemoteServer>();
      srv.Name = name;
      srv.Options = options;
      srv.SiteUrl = siteUrl;
      srv.AuthorizationUrl = authorizationUrl;
      srv.TokenRequestUrl = tokenRequestUrl;
      srv.TokenRefreshUrl = tokenRefreshUrl;
      srv.Scopes = scopes;
      srv.DocumentationUrl = documentationUrl;
      srv.BasicProfileUrl = basicProfileUrl;
      srv.ProfileUserIdTag = profileUserIdTag;
      return srv; 
    } 

  }
}
