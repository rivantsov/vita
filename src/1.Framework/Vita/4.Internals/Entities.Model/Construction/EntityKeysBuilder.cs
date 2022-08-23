using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {

  public partial class EntityKeysBuilder {
    EntityModelBuilder _modelBuilder;
    EntityModel _model => _modelBuilder.Model;
    ILog _log => _modelBuilder.Log; 

    internal EntityKeysBuilder(EntityModelBuilder modelBuilder) {
      _modelBuilder = modelBuilder;
    }

    internal bool BuildKeys() {
      var allKeys = _model.Entities.SelectMany(e => e.Keys).ToList();
      var keysToBuild = allKeys.Where(key => key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      while (keysToBuild.Count > 0) {
        var newList = BuildKeys(keysToBuild);
        _modelBuilder.CheckErrors();
        if (newList.Count == keysToBuild.Count) { // no progress
          ReportErrorFailedToExpandKeys(newList);
          return false;
        }
        keysToBuild = newList; 
      }
      // process other keys
      var otherKeys = allKeys.Where(key => !key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      foreach (var key in otherKeys)
        ExpandOtherKey(key);
      _modelBuilder.CheckErrors();
      return true;
    }

    internal List<EntityKeyInfo> BuildKeys(List<EntityKeyInfo> currentList) {
        var newList = new List<EntityKeyInfo>();
        foreach (var key in currentList) 
          if (!TryBuildPrimaryOrForeignKey(key))
            newList.Add(key);
      return newList; 
    }

    private bool TryBuildPrimaryOrForeignKey(EntityKeyInfo key) {
      if (key.IsExpanded())
        return true;
      if (key.KeyType.IsSet(KeyType.ForeignKey))  //foreign key should be first
        return TryBuildForeignKey(key);
      else
        return TryBuildPrimaryKey(key); 
    }

    private bool TryBuildPrimaryKey(EntityKeyInfo key) {
      if (key.KeyMembers.Count == 0) {

      }
      // check if there are any ref members in the key that are not expanded.
      var hasNotExpandedRefs = key.KeyMembers.Any(km => km.Member.Kind == EntityMemberKind.EntityRef && !km.Member.ReferenceInfo.ToKey.IsExpanded());
      if (hasNotExpandedRefs)
        return false;

      return true; 
    }

    private bool TryBuildForeignKey(EntityKeyInfo key) {
      var refMember = key.OwnerRefMember;
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
      }

    }


    private void ReportErrorFailedToExpandKeys(List<EntityKeyInfo> keys) {

    }

  }
}
