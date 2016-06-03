using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.OAuthClient.Api {
  public class OAuthServerInfo {
    public string Name;
    public OAuthServerType ServerType;
    public string AuthorizationUrl;
    public string TokenRequestUrl;
    public string TokenRefreshUrl;
  }

  public class OAuthServerAccount {
    public Guid Id;
    public OAuthServerInfo Server;
    public string Name;
    public string ClientId;
    public string ClientSecret;
  }

  public class OAuthClientFlow {
    public Guid Id;
    public OAuthServerAccount Account;
    public Guid? UserId;
    public string RedirectUrlBase;
    public string RedirectUrl;
  }

  public class OAuthAccessTokenInfo {
    public Guid AccountId;
    public Guid? UserId;
    public string Token;
    public DateTime ExpiresOn;
    public string RefreshToken;
  }

  public class OAuthRedirectResult {
    public string Error;
    public OAuthAccessTokenInfo AccessToken;
  }



}
