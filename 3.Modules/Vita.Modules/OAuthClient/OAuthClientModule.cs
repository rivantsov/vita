using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClient {

  public partial class OAuthClientModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("1.1.0.0");

    public readonly OAuthClientSettings Settings;

    public OAuthClientModule(EntityArea area, OAuthClientSettings settings = null) : base(area, "OAuthClient", version: CurrentVersion) {
      Settings = settings ?? new OAuthClientSettings();
      this.App.RegisterConfig(this.Settings); 
      RegisterEntities(typeof(IOAuthRemoteServer), typeof(IOAuthRemoteServerAccount), typeof(IOAuthRemoteServerAccessToken), 
          typeof(IOAuthClientFlow), typeof(IOAuthOpenIdToken));
      App.RegisterService<IOAuthClientService>(this); 
    }
  }

}
