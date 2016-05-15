using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClientData {

    /// <summary>Contains information about remote OAuth server. </summary>
    [Entity]
    public interface IOAuthServer {
      [PrimaryKey, Auto]
      Guid Id { get; }
      [Auto(AutoType.CreatedOn), Utc]
      DateTime CreatedOn { get; }
      
      [Size(50)]
      string Name { get; set; }

      IEncryptedData ClientIdentifier { get; set; }
      IEncryptedData ClientSecret { get; set; }

      //URLs
      [Size(OAuthClientDataModule.OAuthUrlSizeCode)]
      string TempCredentialRequestUrl { get; set; }
      
      [Size(OAuthClientDataModule.OAuthUrlSizeCode)]
      string AuthorizationUrl { get; set; } // Resource Owner Authorization URI

      [Size(OAuthClientDataModule.OAuthUrlSizeCode)]
      string TokenRequestUrl { get; set; }
    }

    [Entity]
    public interface IOAuthTempCredentials {
      [PrimaryKey, Auto]
      Guid Id { get; }

      [Auto(AutoType.CreatedOn), Utc]
      DateTime CreatedOn { get; }

      IOAuthServer Server { get; set; }

      Guid? UserId { get; set; }

      [Size(OAuthClientDataModule.OAuthTokenSizeCode)]
      string TempToken { get; set; }
      [Size(OAuthClientDataModule.OAuthTokenSizeCode)]
      string TempSecret { get; set; }

      //returned by server after user confirmed; also can contain Pin code that user enters manually (by copying from server's login page)
      [Size(OAuthClientDataModule.OAuthTokenSizeCode), Nullable] 
      string Verifier { get; set; }

      [Utc]
      DateTime ExpiresOn { get; set; }

    }

    [Entity]
    public interface IOAuthCredentials {
      [PrimaryKey, Auto]
      Guid Id { get; }
      IOAuthServer Server { get; set; }
      Guid UserId { get; set; }
      IEncryptedData AuthorizationToken { get; set; }
      IEncryptedData AuthorizationSecret { get; set; }
      string RemoteUserId { get; set; }
      [Utc]
      DateTime ExpiresOn { get; set; }
    }
  
}//ns
