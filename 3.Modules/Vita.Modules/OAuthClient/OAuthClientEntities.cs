using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClient {

  [Flags]
  public enum OAuthServerOptions {
    None = 0,
    OpenIdConnect = 1, // supports OpenId Connect, derivation of OAuth2

    // Access token endpoint options
    /// <summary>Access token endpoint uses GET HTTP method. Non-standard option, stardard requires POST.</summary>
    TokenUseGet = 1 << 8, 
    /// <summary>Access token endpoint expects Authorization header with Basic scheme and Base64-encoded 
    /// string [client_id:clientsecret]. </summary>
    TokenUseAuthHeaderBasic64 = 1 << 9, 
    /// <summary>Access token endpoint expects parameters in form-encoded body.</summary>
    TokenUseFormUrlEncodedBody = 1 << 10,

    /// <summary>
    /// Replace IP address 127.0.0.1 with localhost. Useful in some cases for testing (Facebook).
    /// </summary>
    /// <remarks>Most servers do not allow localhost as redirect URI, so we use local IP (127.0.0.1) for testing. 
    /// Facebook does not allow either in allowed Domains, but it does allow specifying localhost as Site URL. 
    /// So with this flag, we replace local IP address with localhost when we test Facebook. 
    /// </remarks>
    TokenReplaceLocalIpWithLocalHost = 1 << 11, 
  }


  /// <summary>Contains information about remote OAuth servers. </summary>
  [Entity]
  public interface IOAuthRemoteServer {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }

    [Size(50), Unique]
    string Name { get; set; }
    OAuthServerOptions Options { get; set; }
    [Size(100)]
    string SiteUrl { get; set; }
    [Size(100)]
    string AuthorizationUrl { get; set; } // Resource Owner Authorization URI
    [Size(100)]
    string TokenRequestUrl { get; set; }
    [Size(100), Nullable]
    string TokenRefreshUrl { get; set; }
    [Unlimited, Nullable]
    string Scopes { get; set; }
    [Size(100), Nullable]
    string DocumentationUrl { get; set; }
    [Size(100), Nullable]
    string BasicProfileUrl { get; set; }

  }

  [Entity]
  public interface IOAuthRemoteServerAccount {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }
    IOAuthRemoteServer Server { get; set; }
    Guid? OwnerId { get; set; } //for multi-tenant apps
    [Size(50)]
    string Name { get; set; }
    [Unlimited]
    string ClientIdentifier { get; set; } //client ID is not a secret
    [GrantAccess]
    IEncryptedData ClientSecret { get; set; }
  }

  public enum OAuthFlowStatus {
    Started,
    Authorized,
    TokenRetrieved,
    Error
  }

  public enum OAuthTokenType {
    Bearer,
    Basic,
  }

  [Entity]
  public interface IOAuthClientFlow {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }

    IOAuthRemoteServerAccount Account { get; set; }
    [Unlimited, Nullable]
    string Scopes { get; set; }
    OAuthFlowStatus Status { get; set; }

    [Unlimited]
    string AuthorizationUrl { get; set; }
    [Unlimited]
    string RedirectUrl { get; set; }

    Guid? UserId { get; set; }
    Guid? UserSessionId { get; set; }

    [Unlimited, Nullable]
    string AuthorizationCode { get; set; }
    [Size(50), Nullable]
    string Error { get; set; }
    [Nullable]
    IOAuthAccessToken Token { get; set; }
  }

  [Entity]
  public interface IOAuthAccessToken {
    [PrimaryKey, Auto]
    Guid Id { get; }
    IOAuthRemoteServerAccount Account { get; set; }
    Guid? UserId { get; set; }
    [GrantAccess]
    IEncryptedData Token { get; set; }
    OAuthTokenType TokenType { get; set; }
    [Unlimited, Nullable]
    string Scopes { get; set; }
    [Utc]
    DateTime RetrievedOn { get; set; }
    [Utc]
    DateTime ExpiresOn { get; set; }
    [Nullable, GrantAccess]
    IEncryptedData RefreshToken { get; set; }
    [Nullable, GrantAccess]
    IOAuthOpenIdToken OpenIdToken { get; set; }
  }

  [Entity]
  public interface IOAuthOpenIdToken {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Size(100), Index]
    string Subject { get; set; }
    [Size(100)]
    string Issuer { get; set; }
    [Unlimited, Nullable]
    string Audience { get; set; }
    [Size(100), Nullable]
    string Nonce { get; set; }
    //  authentication context reference, 'acr'
    [Size(100), Nullable]
    string AuthContextRef { get; set; }
    DateTime? AuthTime { get; set; }
    DateTime IssuedAt { get; set; }
    DateTime ExpiresAt { get; set; }

    [Unlimited]
    string FullJson { get; set; }
  }
}//ns
