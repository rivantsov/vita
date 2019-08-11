using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Data.Model;
using Vita.Data.Upgrades;

namespace Vita.Modules.Logging {

  public static class DbUpgradeLogExtensions {

    public static bool CanLogModelChanges(this DbUpgradeInfo changeSet) {
      var hasChanges = changeSet.EntitiesChanging(DbUpgradeLogModule.EntityTypes);
      return !hasChanges;
    }

    public static IDbUpgradeBatch NewDbModelChangeBatch(this IEntitySession session,
                                                string fromVersion, string toVersion, DateTime startedOn, DateTime? completedOn, 
                                                DbUpgradeMethod method, string machineName, string userName, 
                                                Exception exception = null) {
      var ent = session.NewEntity<IDbUpgradeBatch>();
      ent.FromVersion = fromVersion;
      ent.ToVersion = toVersion;
      ent.StartedOn = startedOn;
      ent.CompletedOn = completedOn; 
      ent.Method =  method;
      ent.MachineName = machineName ?? Environment.MachineName;
      ent.UserName = userName ?? Environment.UserName;
      if (exception == null)
        ent.Success = true;
      else {
        ent.Success = false;
        ent.Errors = exception.ToLogString(); 
      }
      return ent; 
    }

    public static IDbUpgradeScript NewDbModelChangeScript(this IDbUpgradeBatch batch, 
                          DbObjectChangeType changeType,  DbObjectType objectType, 
                          string fullObjectName,
                          string sql, int executionOrder, int subOrder, int duration, Exception exception) {
      var session = EntityHelper.GetSession(batch);
      var ent = session.NewEntity<IDbUpgradeScript>();
      ent.Batch = batch;
      ent.ObjectType = objectType;
      ent.FullObjectName = fullObjectName;
      ent.ExecutionOrder = executionOrder;
      ent.SubOrder = subOrder;
      ent.Sql = sql;
      ent.Duration = duration;
      if(exception != null)
        ent.Errors = exception.ToLogString();
      return ent; 
    }

    public static IDbUpgradeScript NewDbModelChangeScript(this IDbUpgradeBatch batch, DbUpgradeScript script, int index = 0, Exception exception = null) {
      var session = EntityHelper.GetSession(batch);
      var ent = session.NewEntity<IDbUpgradeScript>();
      ent.Batch = batch;
      if (script.Migration != null) {
        ent.ObjectType = DbObjectType.Other;
        ent.FullObjectName = script.Migration.Name;
      } else if (script.ModelChange != null) {
        ent.ObjectType = script.ModelChange.ObjectType;
        ent.FullObjectName = script.ModelChange.DbObject.LogRefName;
      } else {
        ent.ObjectType = DbObjectType.Other;
        ent.FullObjectName = "(Unknown)";
      }
      ent.ExecutionOrder = index;
      ent.Sql = script.Sql;
      ent.Duration = script.Duration;
      if(exception != null)
        ent.Errors = exception.ToLogString();
      return ent;
    }

  }
}
