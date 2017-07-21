using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Data.Upgrades;

using Vita.Modules.EncryptedData;

namespace Vita.Modules.OAuthClient {
  public partial class OAuthClientModule {
    public static bool SuppressMigrationErrors = false; 

    public override void RegisterMigrations(DbMigrationSet migrations) {
      migrations.AddPostUpgradeAction("1.3.0.0", "CreateDefaultServers", "Creates records for popular OAuth servers",
          session => OAuthServers.CreateUpdatePopularServers(session));
      if(!migrations.IsNewInstall()) {
        migrations.AddPostUpgradeAction("1.3.0.0", "UnencryptOAuthTokens", "Switch tokens to store unencrypted values",
            session => UnencryptTokens(session));
      }
    }

    // If necessary, can be called explicitly outside of migrations to unencrypt existing values
    public static void UnencryptTokens(IEntitySession session) {
      var config = session.Context.App.GetConfig<OAuthClientSettings>();
      var errLog = session.Context.App.ErrorLog;
      // Unencrypt client secret in remote server accounts - should be a smaill table
      var accts = session.EntitySet<IOAuthRemoteServerAccount>().Where(a => a.ClientSecret_Id != null).Take(100).ToList(); 
      foreach(var acct in accts) {
        var ed = session.GetEntity<IEncryptedData>(acct.ClientSecret_Id);
        if(ed == null)
          continue;
        acct.ClientSecret = ed.DecryptString(config.EncryptionChannel);
        acct.ClientSecret_Id = null; 
      }
      session.SaveChanges(); 

      // Tokens - might be big table, process in batches
      int batchSize = 200;
      int skip = 0;
      try {
        var tokenRecs = session.EntitySet<IOAuthAccessToken>()
          .Where(tr => tr.AccessToken == null).OrderBy(tr => tr.RetrievedOn)
          .Skip(skip).Take(batchSize).ToList();
        skip += batchSize;
        if(tokenRecs.Count == 0)
          return;
        //preload all EncryptedValue records
        var encrIds1 = tokenRecs.Where(tr => tr.AccessToken_Id != null).Select(tr => tr.AccessToken_Id.Value).ToList();
        var encrIds2 = tokenRecs.Where(tr => tr.RefreshToken_Id != null).Select(tr => tr.RefreshToken_Id.Value).ToList();
        encrIds1.AddRange(encrIds2); 
        var encrRecs = session.EntitySet<IEncryptedData>().Where(ed => encrIds1.Contains(ed.Id));
        foreach(var tr in tokenRecs) {
          //Access token
          var eId = tr.AccessToken_Id;
          if(eId != null && eId.Value != Guid.Empty) {
            var ed = session.GetEntity<IEncryptedData>(eId.Value); //should be preloaded
            if(ed != null) {
              tr.AccessToken = ed.DecryptString(config.EncryptionChannel);
              tr.AccessToken_Id = null; 
            }
          }
          // Refresh token
          eId = tr.AccessToken_Id;
          if(eId != null && eId.Value != Guid.Empty) {
            var ed = session.GetEntity<IEncryptedData>(eId.Value); //should be preloaded
            if(ed != null) {
              tr.RefreshToken = ed.DecryptString(config.EncryptionChannel);
              tr.RefreshToken_Id = null;
            }
          }
        } //foreach tr
        session.SaveChanges();
      } catch(Exception ex) {
        if(errLog != null)
          errLog.LogError(ex, session.Context);
        if (!SuppressMigrationErrors)
          throw; 
      }
    } //method

  } //class
} //ns
