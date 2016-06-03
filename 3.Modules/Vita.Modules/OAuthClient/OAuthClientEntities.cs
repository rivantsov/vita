using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClient {

  public enum OAuthServerType {
    OAuth2,
    OpenIdConnect, //derivation of OAuth2
    Facebook,  //Fb flavor
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
    OAuthServerType ServerType { get; set; }

    [Size(100)]
    string AuthorizationUrl { get; set; } // Resource Owner Authorization URI
    [Size(100)]
    string TokenRequestUrl { get; set; }
    [Size(100)]
    string TokenRefreshUrl { get; set; }
    [Unlimited, Nullable]
    string Scopes { get; set; }
  }

  [Entity]
  public interface IOAuthRemoteServerAccount {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; }
    IOAuthRemoteServer Server { get; set; }
    Guid? OwnerId { get; set; }
    [Size(50)]
    string Name { get; set; }
    IEncryptedData ClientIdentifier { get; set; }
    IEncryptedData ClientSecret { get; set; }
  }

  public enum OAuthClientFlowStatus {
    Started,
    Success,
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
    OAuthClientFlowStatus Status { get; set; }

    [Size(100)]
    string RedirectUrl { get; set; }

    Guid? UserId { get; set; }
    Guid? UserSessionId { get; set; }

    [Size(100), Nullable]
    string Error { get; set; }
    [Size(100)]
    string AuthorizationCode {get;set;}
    [Nullable]
    IOAuthRemoteServerAccessToken Token { get; set; }
  }

  [Entity]
  public interface IOAuthRemoteServerAccessToken {
    [PrimaryKey, Auto]
    Guid Id { get; }
    IOAuthRemoteServerAccount Account { get; set; }
    Guid? UserId { get; set; }
    IEncryptedData AuthorizationToken { get; set; }
    OAuthTokenType TokenType { get; set; }
    [Utc]
    DateTime RetrievedOn { get; set; }
    [Utc]
    DateTime ExpiresOn { get; set; }
    IEncryptedData RefreshToken { get; set; }
    [Nullable]
    IOAuthOpenIdToken OpenIdToken { get; set; }
  }

  [Entity]
  public interface IOAuthOpenIdToken {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Size(100), Index]
    string Subject { get; set; }
    [Size(50)]
    string Issuer { get; set; }
    [Size(50), Nullable]
    string Audience { get; set; }
    [Size(50), Nullable]
    string Nonce { get; set; }
    //  authentication context reference, 'acr'
    [Size(50)]
    string AuthContextRef { get; set; }
    DateTime AuthTime { get; set; }
    DateTime IssuedAt { get; set; }
    DateTime ExpiresAt { get; set; }

  }
}//ns
