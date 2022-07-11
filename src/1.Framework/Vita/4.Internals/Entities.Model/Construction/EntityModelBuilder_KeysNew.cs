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
      var keysToExpand = Model.Entities.SelectMany(e => e.Keys).ToList();
      do {
        var newList = new List<EntityKeyInfo>();
        foreach (var key in keysToExpand)
          if (TryExpandKey(key))
            continue;
          else
            newList.Add(key);
        // check if we are stuck, report error and exit if we are
        if (newList.Count == keysToExpand.Count) {
          ReportErrorFailedToExpandKeys(keysToExpand);
          return false; 
        }
        keysToExpand = newList; 
      } while (keysToExpand.Count > 0);
      CheckErrors();
      return true; 
    }

    private bool TryExpandKey(EntityKeyInfo key) {
      if (key.IsExpanded())
        return true;
      if (key.KeyType.IsSet(KeyType.ForeignKey))
        return TryExpandForeignKey(key);
      else
        return TryExpandRegularKey(key); 
    }

    private bool TryExpandRegularKey(EntityKeyInfo key) {

    }

    private bool TryExpandForeignKey(EntityKeyInfo key) {
      var refMember = key.OwnerMember;
      var toKey = refMember.ReferenceInfo.ToKey;
      if (!toKey.IsExpanded())
          return false;

      var nullable = refMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var isPk = refMember.Flags.IsSet(EntityMemberFlags.PrimaryKey);
    }

    private void ReportErrorFailedToExpandKeys(List<EntityKeyInfo> keys) {

    }

  }
}
