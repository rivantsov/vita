using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Data.Model;
using Vita.Data.Driver;

namespace Vita.Data.Upgrades {

  public static class DManagementbExtensions {


    public static bool IsSet(this DbScriptOptions flags, DbScriptOptions flag) {
      return (flags & flag) != 0;
    }

    public static bool EntitiesChanging(this DbUpgradeInfo upgradeInfo, params Type[] entities) {
      foreach(var tg in upgradeInfo.TableChanges) {
        if(tg.NewTable == null)
          continue; 
        if(entities.Contains(tg.NewTable.Entity.EntityType))
          return true; 
      }
      return false; 
    }

    public static string GetAllAsText(this IList<DbUpgradeScript> scripts) {
      var result = string.Join(Environment.NewLine, scripts.Select(s => s.Sql));
      return result;
    }

  }//class

}//namespace
