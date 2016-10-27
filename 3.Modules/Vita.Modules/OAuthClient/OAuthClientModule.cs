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

  }

}
