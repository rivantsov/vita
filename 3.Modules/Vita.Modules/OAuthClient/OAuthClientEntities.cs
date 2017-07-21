using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.OAuthClient {

  public enum OAuthFlowStatus {
    Started,
    Authorized,
    TokenRetrieved,
    Error,
    Expired,
  }

  public enum OAuthTokenType {
    Bearer,
    Basic,
  }

  /// <summary>Status of OAuth token.</summary>
  public enum OAuthTokenStatus {
    /// <summary>Token is active and can be used to access the target server. </summary>
    Active = 0,
    /// <summary>Token expired. </summary>
    Expired, 
    /// <summary>Token was revoked by OAuth client by calling server API endpoint. </summary>
    Revoked,
    /// <summary>The server rejected an access attempt, most likely because the user revoked access permissions
    /// for the client app at the target server Web site. </summary>
    Rejected,
  }

  [Flags]
  public enum OAuthServerOptions {
    None = 0,
    OpenIdConnect = 1, // supports OpenId Connect, derivation of OAuth2

    // Access token endpoint options
    /// <summary>Access token endpoint uses GET HTTP method. Non-standard option, stardard requires POST.</summary>
    TokenUseGet = 1 << 8, 
    /// <summary>Access token endpoint expects Authorization header with Basic scheme and Base64-encoded 
    /// string [client_id:clientsecret]. </summary>
    ClientInfoInAuthHeader = 1 << 9, 

    /// <summary>
    /// Replace IP address 127.0.0.1 with localhost. Useful in some cases for testing (Facebook, WindowsLive).
    /// </summary>
    /// <remarks>Most servers do not allow localhost as redirect URI, so we use local IP (127.0.0.1) for testing. 
    /// Facebook does not allow either in allowed Domains, but it does allow specifying localhost as Site URL. 
    /// So with this flag, we replace local IP address with localhost when we test Facebook. 
    /// </remarks>
    TokenReplaceLocalIpWithLocalHost = 1 << 11,

    /// <summary>Use GET method for revoke-token endpoint; by default POST is used.</summary>
    RevokeUseGetNoClientInfo = 1 << 12,

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
    [Size(100), Nullable]
    string TokenRevokeUrl { get; set; }
    [Unlimited, Nullable]
    string Scopes { get; set; }
    [Size(100), Nullable]
    string DocumentationUrl { get; set; }
    [Size(100), Nullable]
    string BasicProfileUrl { get; set; }
    [Size(100), Nullable]
    string ProfileUserIdTag { get; set; }
  }

  [Entity]
  public interface IOAuthRemoteServerAccount {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }
    [GrantAccess]
    IOAuthRemoteServer Server { get; set; }
    [Size(50)]
    string Name { get; set; }
    [Unlimited]
    string ClientIdentifier { get; set; } //client ID is not a secret

    // New column - store unencrypted
    [Size(250), Nullable]
    string ClientSecret { get; set; }
    // Deprecated - old reference to EncryptedData record
    Guid? ClientSecret_Id { get; set; }
  }

  [Entity]
  public interface IOAuthClientFlow {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }
    [GrantAccess]
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
    [Size(100), Nullable]
    string Error { get; set; }
    [Nullable, GrantAccess]
    IOAuthAccessToken Token { get; set; }
  }

  [Entity]
  public interface IOAuthAccessToken {
    [PrimaryKey, Auto]
    Guid Id { get; }
    IOAuthRemoteServerAccount Account { get; set; }
    Guid? UserId { get; set; }
    OAuthTokenStatus Status { get; set; }
    OAuthTokenType TokenType { get; set; }
    [Unlimited, Nullable]
    string Scopes { get; set; }
    [Utc]
    DateTime RetrievedOn { get; set; }
    [Utc]
    DateTime ExpiresOn { get; set; }
    [Utc]
    DateTime? RefreshedOn { get; set; }
    [Nullable, GrantAccess]
    IOAuthOpenIdToken OpenIdToken { get; set; }

    // Tokens: switching from old schema (storing encrypted in EnryptedData) to new - storing unencrypted value directly
    [Nullable, Unlimited]
    string AccessToken { get; set; }
    [Nullable, Unlimited]
    string RefreshToken { get; set; }
    // Deprecated - left for now to keep reference to EncryptedData to use in migrations to unencrypt old data
    Guid? RefreshToken_Id { get; set; }
    Guid? AccessToken_Id { get; set; }
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

  [Entity, Unique("ExternalUserId,Server")]
  public interface IOAuthExternalUser {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Utc, Auto(AutoType.CreatedOn)]
    DateTime CreatedOn { get; set; }
    IOAuthRemoteServer Server { get; set; }

    [Index]
    Guid UserId { get; set; }
    [Size(100)]
    string ExternalUserId {get;set;}
  }

}//ns
