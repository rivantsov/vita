using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Upgrades;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClient {

  public partial class OAuthClientModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("1.2.1.0");

    public readonly OAuthClientSettings Settings;

    public OAuthClientModule(EntityArea area, OAuthClientSettings settings = null) : base(area, "OAuthClient", version: CurrentVersion) {
      Settings = settings ?? new OAuthClientSettings(); 
      this.App.RegisterConfig(this.Settings); 
      RegisterEntities(typeof(IOAuthRemoteServer), typeof(IOAuthRemoteServerAccount), typeof(IOAuthAccessToken), 
          typeof(IOAuthClientFlow), typeof(IOAuthOpenIdToken), typeof(IOAuthExternalUser));
      App.RegisterService<IOAuthClientService>(this);
      Requires<EncryptedData.EncryptedDataModule>();
    }

    public override void RegisterMigrations(DbMigrationSet migrations) {
      base.RegisterMigrations(migrations);
      migrations.AddPostUpgradeAction("1.2.1.0", "CreateDefault", "Creates records for popular OAuth servers", 
          session => OAuthServers.CreateUpdatePopularServers(session));
    }

    public override void WebInitialize(WebCallContext webContext) {
      base.WebInitialize(webContext);
      if (string.IsNullOrWhiteSpace(Settings.RedirectUrl)) {
        // Initialize RedirectUrl; get service base address and set proper redirect URL
        // It is convenient to do it automatically here - so that it works automatically in real world apps, in any environment - test, staging or production. 
        var uri = new Uri(webContext.RequestUrl);
        var baseAddress = uri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort, UriFormat.Unescaped);
        var baseAddressR = baseAddress.Replace("localhost", "127.0.0.1"); //By default we use IP address for local testing, this is required for most oauth servers
        Settings.RedirectUrl = baseAddressR + "/api/oauth_redirect";
      }
    }//method

  }

}
