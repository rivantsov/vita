using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 
using Vita.Data.Upgrades;
using Vita.Modules.EncryptedData;

namespace Vita.Modules.Login {
  public partial class LoginModule {

    public static bool SuppressMigrationErrors = false;

    public override void RegisterMigrations(DbMigrationSet migrations) {
      if (!migrations.IsNewInstall()) {
        migrations.AddPostUpgradeAction("1.2.0.0", "FactorsPlainValue", "Switch extra factors to storing unencrypted values",
            s => UnencryptFactorValues(s));
      }
    }

    // Can be called explicitly outside of migrations to unencrypt existing values
    public static void UnencryptFactorValues(IEntitySession session) {
      var loginConfig = session.Context.App.GetConfig<LoginModuleSettings>();
      var errLog = session.Context.App.ErrorLog;
      int batchSize = 200;
      int skip = 0; 
      try {
        var factors = session.EntitySet<ILoginExtraFactor>()
          .Where(f => f.FactorValue == null).OrderBy(f => f.CreatedOn)
          .Skip(skip).Take(batchSize).ToList();
        skip += batchSize;
        if(factors.Count == 0)
          return;
        //preload all EncryptedValue records
        var encrIds = factors.Select(f => f.Info_Id).ToList();
        var encrRecs = session.EntitySet<IEncryptedData>().Where(ed => encrIds.Contains(ed.Id));
        foreach(var f in factors) {
          if(f.Info_Id == null || f.Info_Id.Value == Guid.Empty)
            continue;
          var ed = session.GetEntity<IEncryptedData>(f.Info_Id); //should be preloaded
          if(ed == null)
            continue;
          f.FactorValue = ed.DecryptString(loginConfig.EncryptionChannelName);
          f.Info_Id = null; 
        }
        session.SaveChanges(); 
      } catch(Exception ex) {
        if(errLog != null)
          errLog.LogError(ex, session.Context);
        if(!SuppressMigrationErrors)
          throw;
      }

    }

  }//class
}//ns
