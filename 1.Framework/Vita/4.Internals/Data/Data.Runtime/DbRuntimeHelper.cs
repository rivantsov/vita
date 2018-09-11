using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  public static class DbRuntimeHelper {

    public static bool ShouldUpdate(EntityRecord record) {
      if(record.Status == EntityStatus.Modified && record.EntityInfo.Flags.IsSet(EntityFlags.NoUpdate))
        return false; //if for whatever reason we have such a record, just ignore it
      if(record.Status == EntityStatus.Fantom)
        return false;
      return true;
    }

    public static EntityOperation GetOperation(this EntityRecord record) {
      switch(record.Status) {
        case EntityStatus.New: return EntityOperation.Insert;
        case EntityStatus.Modified: return EntityOperation.Update;
        case EntityStatus.Deleting: return EntityOperation.Delete;
        case EntityStatus.Loaded: case EntityStatus.Stub: return EntityOperation.Select;
        default:
          Util.Throw("Invalid EntityRecord status for DbOperation. Record: {0}", record);
          return default(EntityOperation); 
      }
    }


    public static IDbCommand Clone(this IDbCommand cmd, DbDriver driver) {
      var clone = driver.CreateCommand();
      clone.CommandText = cmd.CommandText;
      for(int i = 0; i < cmd.Parameters.Count; i++) {
        var p = (IDbDataParameter)cmd.Parameters[i];
        var cloneP = clone.CreateParameter();
        driver.CopyParameterSetup(p, cloneP);
        clone.Parameters.Add(cloneP);
      }
      return clone;
    }

  } //class
}
