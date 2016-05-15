using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClientData {

  public class OAuthClientDataModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public const string OAuthTokenSizeCode = "OAuthToken";
    public static int DefaultOAuthTokenSize = 200;
    public const string OAuthUrlSizeCode = "OAuthUrl";
    public static int DefaultOAuthUrlSize = 100;

    public OAuthClientDataModule(EntityArea area) : base(area, "OAuthData", version: CurrentVersion) {
      RegisterEntities(typeof(IOAuthServer), typeof(IOAuthCredentials), typeof(IOAuthTempCredentials));
      RegisterSize(OAuthTokenSizeCode, DefaultOAuthTokenSize);
      RegisterSize(OAuthUrlSizeCode, DefaultOAuthUrlSize);
    }
  }

}
