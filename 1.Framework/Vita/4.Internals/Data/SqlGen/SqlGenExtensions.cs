using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.SqlGen {

  public static partial class SqlGenExtensions {

    public static bool CheckReferencesNewIdentity(this EntityRecord rec, DbColumnInfo fkCol, out EntityRecord targetRecord) {
      targetRecord = null;
      if(!rec.EntityInfo.Flags.IsSet(EntityFlags.ReferencesIdentity))
        return false; 
      if(!fkCol.Flags.IsSet(DbColumnFlags.IdentityForeignKey))
        return false; 
      var targetRef = rec.GetValueDirect(fkCol.Member.ForeignKeyOwner);
      if(targetRef == DBNull.Value)
        return false;
      var targetRec = (EntityRecord)targetRef;
      if(targetRec.Status != EntityStatus.New)
        return false;
      targetRecord = targetRec;
      return true;
    }

    public static void AddMany(this IList<SqlFragment> fragments, params SqlFragment[] list) {
      foreach(var fr in list)
        fragments.Add(fr); 
    }

  }
}
