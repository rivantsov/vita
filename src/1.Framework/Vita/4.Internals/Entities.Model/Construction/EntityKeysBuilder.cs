using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {

  //  !!  See EntityKeysBuilder_ReadMe.md for more info about this class 

  internal partial class EntityKeysBuilder {
    EntityModelBuilder _modelBuilder;
    internal EntityModel Model => _modelBuilder.Model;
    ILog _log; 
    List<EntityKeyInfo> _allKeys;
    List<EntityKeyInfo> _pkFkKeys;

    internal EntityKeysBuilder(EntityModelBuilder modelBuilder) {
      _modelBuilder = modelBuilder;
      _log = _modelBuilder.Log; 
    }

    internal void BuildKeys() {
      _allKeys = Model.Entities.SelectMany(e => e.Keys).ToList();
      ParseKeySpecs();
      _modelBuilder.CheckErrors();
      // process simple 1-column PKs if any.
      _pkFkKeys = _allKeys.Where(key => key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      InitSimplePks(); //one-column PKs
      // run main loop
      if (!RunKeyBuildLoop())
        return; 

    } // method

    private bool RunKeyBuildLoop() {
      var workList = _allKeys.Where(key => key.BuildStatus != KeyBuildStatus.MembersExpanded).ToList();
      // run loop several times:
      //   1. try process FKs, this can adds columns mapped from target FK
      //   2. try process PKs
      while (true) {
        var oldKeyCount = workList.Count;

        // refresh work list
        workList = _allKeys.Where(key => key.BuildStatus != KeyBuildStatus.MembersExpanded).ToList();
        if (workList.Count == 0)
          return true; 
        if (workList.Count == oldKeyCount) { //count did not change, we make no progress, it is error
          var keyList = string.Join(",", workList.Select(km => km.GetSafeKeyRef()));
          _log.LogError(
            @$"FATAL: Key builder process could not complete, remaining key count: {workList.Count}. Keys: {keyList}");
          return false; 
        }
      }
    }

    private void ParseKeySpecs() {
      foreach(var key in _allKeys) {
        if (!string.IsNullOrEmpty(key.MemberListSpec))
          ParseKeySpec(key);
      }
    }

    private void InitSimplePks() {
      var simplePks = _pkFkKeys.Where(key => key.KeyType.IsSet(KeyType.PrimaryKey) && 
                   key.OwnerMember != null && key.OwnerMember.Kind == EntityMemberKind.Column);
      foreach (var key in simplePks) {
        var keyMember = new EntityKeyMemberInfo(key.OwnerMember);
        key.KeyMembers.Add(keyMember);
        key.ExpandedKeyMembers.Add(keyMember);
        key.BuildStatus = KeyBuildStatus.MembersExpanded;
      }
    }


    // entity and specHolder are the same in most cases. Except for [OrderBy] attribute
    //  on list property. In this case 'entity' is target entity being ordered, and specHolder
    //  is entity that hosts the list property and attribute
    public void ParseKeySpec(EntityKeyInfo key) {
      var ent = key.Entity;
      var allowAscDesc = key.KeyType.IsSet(KeyType.Index | KeyType.PrimaryKey);
      var spec = key.MemberListSpec;
      var success = true;
      var segments = StringHelper.SplitNames(key.MemberListSpec);
      foreach (var segm in segments) {
        string[] parts;
        parts = StringHelper.SplitNames(segm, ':');
        if (parts.Length > 2) {
          _log.LogError($"Key '{spec}', entity {ent}: Invalid segment '{segm}'; expected format: 'member[:desc]'.");
          success = false;
          continue;
        }
        var memberName = parts[0];
        if (string.IsNullOrWhiteSpace(memberName)) {
          _log.LogError($"Key '{spec}', entity {ent}: Invalid segment '{segm}';  member name may not be null.");
          success = false;
          continue; 
        }
        bool desc = false;
        string strDesc = parts.Length == 1 ? "asc" : parts[1];
        switch (strDesc.Trim().ToLowerInvariant()) {
          case "": case "asc": desc = false; break;
          case "desc": desc = true; break;
          default:
            _log.LogError($"Key '{spec}', entity {ent}: Invalid segment '{segm}'; Expected ':asc' or ':desc' as direction specification.");
            success = false;
            continue;
        }//switch

        if (parts.Length > 1 && !allowAscDesc) {
          _log.LogError($"Key '{spec}', entity {ent}: Invalid segment '{segm}';  asc/desc specifier not allowed for this key/index type.");
          success = false;
          continue;
        }
        var member = ent.GetMember(memberName); //might be null 
        key.KeyMembers.Add(new EntityKeyMemberInfo(memberName, member, desc));
      }//foreach segm
      //set build status
      if (success && key.KeyMembers.All(km => km.Member != null))
        key.BuildStatus = KeyBuildStatus.MembersFilled;
    } //foreach segm

  }

  // =============================== Helper =======================================

  internal static class KeyBuilderHelper {

    public static string GetSafeKeyRef(this EntityKeyInfo key) {
      return $"{key.Entity.Name}/{key.KeyType}";
    }
  }
}
