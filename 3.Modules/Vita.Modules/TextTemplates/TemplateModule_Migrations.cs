using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Upgrades;
using Vita.Entities; 

namespace Vita.Modules.TextTemplates {

  public partial class TemplateModule {
    public override void RegisterMigrations(DbMigrationSet migrations) {
      migrations.AddPostUpgradeAction("1.1.0.0", "TextTemplateNullableOwner", 
          "TextTemplate: Change OwnerId to Nullable, change all Guid.Empty values to NULL.",  
        session => {
          var query = session.EntitySet<ITextTemplate>().Where(tt => tt.OwnerId == Guid.Empty).Select(tt => new { Id = tt.Id, OwnerId = (Guid?)null });
          query.ExecuteUpdate<ITextTemplate>();
        });
      /*
      // Other method: using SQL-based migration command; this might be limited to certain server type
      // Notice the use of GetFullTableName helper function
      if (migrations.ServerType == Data.Driver.DbServerType.MsSql) {
        var sql = string.Format("UPDATE {0} SET OwnerId = NULL WHERE OwnerId = '00000000-0000-0000-0000-000000000000';", migrations.GetFullTableName<ITextTemplate>());
        migrations.AddSql("1.0.1.0", "NullableOwner", "Changed OwnerId to Nullable", sql);      
      }
       */
    }//method
  }
}
