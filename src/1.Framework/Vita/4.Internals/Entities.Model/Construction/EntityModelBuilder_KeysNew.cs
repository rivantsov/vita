using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {
  // key expansion methods
  public partial class EntityModelBuilder {

    private bool ExpandEntityKeyMembersNew() {
      var allKeys = Model.Entities.SelectMany(e => e.Keys).ToList();
      var pkFkList = allKeys.Where(key => key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      do {
        var newList = new List<EntityKeyInfo>();
        foreach (var key in pkFkList) {
          if (TryExpandPrimaryOrForeignKey(key))
            continue;
          newList.Add(key);
        }
        // check if we are stuck, report error and exit if we are
        if (newList.Count == pkFkList.Count) { // no progress
          ReportErrorFailedToExpandKeys(pkFkList);
          return false; 
        }
        pkFkList = newList; 
      } while (pkFkList.Count > 0);
      CheckErrors();

      // process other keys
      var otherKeys = allKeys.Where(key => !key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      foreach (var key in otherKeys)
        ExpandOtherKey(key);
      CheckErrors();
      return true; 
    }

    private bool TryExpandPrimaryOrForeignKey(EntityKeyInfo key) {
      if (key.IsExpanded())
        return true;
      if (key.KeyType.IsSet(KeyType.ForeignKey))
        return TryExpandForeignKey(key);
      else
        return TryExpandPrimaryKey(key); 
    }

    private bool TryExpandPrimaryKey(EntityKeyInfo key) {
      // check if there are any ref members in the key that are not expanded.
      var hasNotExpandedRefs = key.KeyMembers.Any(km => km.Member.Kind == EntityMemberKind.EntityRef && !km.Member.ReferenceInfo.ToKey.IsExpanded());
      if (hasNotExpandedRefs)
        return false; 

    }

    private bool TryExpandForeignKey(EntityKeyInfo key) {
      var refMember = key.OwnerMember;
      var toKey = refMember.ReferenceInfo.ToKey;
      if (!toKey.IsExpanded())
          return false;

      var nullable = refMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var isPk = refMember.Flags.IsSet(EntityMemberFlags.PrimaryKey);
    }

    private void ExpandOtherKey(EntityKeyInfo key) {
      // check if there are any ref members in the key that are not expanded.
      var notExpandedRefs = key.KeyMembers.Where(km => km.Member.Kind == EntityMemberKind.EntityRef && !km.Member.ReferenceInfo.ToKey.IsExpanded()).ToList();
      if (notExpandedRefs.Count > 0) {
        Log.LogError($"FATAL: cannot expand regular key {key.GetFullRef()} ");
        return false;
      }

    }


    private void ReportErrorFailedToExpandKeys(List<EntityKeyInfo> keys) {

    }

  }
}
