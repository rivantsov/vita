using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {

  static partial class MemberLoadHelper {

    internal static IList<EntityRecord> GetRecordsWithUnloadedRefMember(this EntitySession session, EntityMemberInfo member) {
      var targetEntity = member.ReferenceInfo.ToKey.Entity;
      var fkMember = member.ReferenceInfo.ToKey.KeyMembersExpanded[0].Member;
      // Filter for records - we choose records that have ref member value null or stub
      Func<EntityRecord, bool> recFilter = (EntityRecord rec) => {
        var mValue = rec.GetRawValue(member);
        if (mValue == DBNull.Value) // already loaded and it is NULL
          return false;
        if (mValue == null) {
          //  returned null means that this member has never been invoked, and target was never looked up. 
          // However, the target rec might be already loaded into current session.  
          // We do not check this here (tried but code is a bit messy); 
          // we simply return true to indicate that the parent record should be included in list, 
          //  so the target FK value will be included in list of IDs sent to the database in the final query. 
          // TODO: Optimize collecting FK values (maybe)
          return true;
        }
        var targetRec = (EntityRecord)mValue;
        return targetRec.Status == EntityStatus.Stub;
      }; //end filter func

      var recsOfType = session.GetLoadedRecordsForEntity(member.Entity, recFilter);
      return recsOfType;
    }

    internal static IList<EntityRecord> GetRecordsWithUnloadedListMember(this EntitySession session, EntityMemberInfo member) {
      // Filter for records 
      Func<EntityRecord, bool> recFilter = (EntityRecord rec) => {
        var mValue = rec.GetRawValue(member);
        if (mValue == null)
          return true;
        var list = (IPropertyBoundList)mValue;
        return !list.IsLoaded;
      }; //end filter func

      var recsOfType = session.GetLoadedRecordsForEntity(member.Entity, recFilter);
      return recsOfType;
    }

    internal static IList<EntityRecord> GetLoadedRecordsForEntity(this EntitySession session, EntityInfo entityInfo,
                     Func<EntityRecord, bool> filter = null) {
      filter ??= x => true;
      var result = new List<EntityRecord>();
      foreach (var de in session.RecordsLoaded.Table) {
        if (de.Key.KeyInfo.Entity != entityInfo)
          continue;
        var rec = (EntityRecord)de.Value.Target;
        if (rec != null && filter(rec))
          result.Add(rec);
      }
      return result;
    }

  }
}
