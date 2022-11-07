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
      BuildKeyMembers();
      _modelBuilder.CheckErrors();
      
      // process simple 1-column PKs if any.
      _pkFkKeys = _allKeys.Where(key => key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      PreprocessKeysOnMembers(); //one-column PKs
      // run main loop
      if (!RunKeyBuildLoop())
        return; 

    } // method

    private void BuildKeyMembers() {
      foreach (var key in _allKeys) {
        if (key.OwnerMember != null) {
          BuildKeyMembersForKeyOnEntityMember(key);
        } else if (!string.IsNullOrEmpty(key.MemberListSpec)) {
          ParseKeySpec(key);
        } else {
          var keyRef = key.GetSafeKeyRef();
          // should never happen, this case should be caught earlier
          _log.LogError($"FATAL: Key {keyRef} has no OwnerMember and no MemberListSpec.");
          continue; 
        }
      }
    }

    // process keys on members; these keys have a single member; some of them can be immediately 
    // expanded (when OwnerMember is a simple column)
    private void BuildKeyMembersForKeyOnEntityMember(EntityKeyInfo key) {
      var keyMember = new EntityKeyMemberInfo(key.OwnerMember);
      key.KeyMembers.Add(keyMember);
      key.BuildStatus = KeyBuildStatus.KeyMembersCreated;
      // if it is a simple column, we can expand it right now
      if (keyMember.Member.Kind == EntityMemberKind.Column) {
        key.ExpandedKeyMembers.Add(keyMember);
        key.BuildStatus = KeyBuildStatus.ExpandedKeyMembersDone;
      }
    }

    private bool RunKeyBuildLoop() {
      var workList = _allKeys.Where(key => key.BuildStatus != KeyBuildStatus.ExpandedKeyMembersDone).ToList();
      // run loop several times:
      //   1. try process FKs, this can adds columns mapped from target FK
      //   2. try process PKs
      while (workList.Count > 0) {
        var oldKeyCount = workList.Count;
        ProcessKeys(workList);
        // refresh work list
        workList = _allKeys.Where(key => key.BuildStatus != KeyBuildStatus.ExpandedKeyMembersDone).ToList();
        if (workList.Count == 0)
          return true; 
        if (workList.Count == oldKeyCount) { //count did not change, we make no progress, it is a fatal error
          var keyList = string.Join(",", workList.Select(km => km.GetSafeKeyRef()));
          _log.LogError(
            @$"FATAL: Key builder process could not complete, remaining key count: {workList.Count}. Keys: {keyList}");
          return false; 
        }
      }
      return true; 
    }

    private void ProcessKeys(IList<EntityKeyInfo> keys) {
      foreach(var key in keys) {
        if (key.KeyType.IsSet(KeyType.ForeignKey)) {
          var toKey = key.OwnerMember.ReferenceInfo.ToKey;
          if (toKey.BuildStatus != KeyBuildStatus.ExpandedKeyMembersDone)
            continue; // cannot expand it yet 
          ProcessForeignKey(key);
          continue; 
        }
        ProcessRegularKey(key);
      }

    } //method




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
        key.BuildStatus = KeyBuildStatus.KeyMembersCreated;
    } //foreach segm


    //FK expansion is a special case - we expand members from target expanded members (of target PrimaryKey)
    private bool ProcessForeignKey(EntityKeyInfo key) {
      var refMember = key.OwnerMember;
      var refInfo = refMember.ReferenceInfo;
      if (!refInfo.ToKey.IsExpanded())
        return false; 
      var nullable = refMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var isPk = refMember.Flags.IsSet(EntityMemberFlags.PrimaryKey);
      // check/expand ToKey
      var toKeyMembers = refInfo.ToKey.ExpandedKeyMembers;
      var propRef = refMember.Entity.EntityType.Name + "." + refMember.MemberName;
      // Check if we have explicitly specified names in ForeignKey attribute
      string[] fkNames = null;
      if (!string.IsNullOrEmpty(refInfo.ForeignKeyColumns)) {
        fkNames = refInfo.ForeignKeyColumns.SplitNames(',', ';');
        if (fkNames.Length != toKeyMembers.Count) {
          _log.LogError($"Invalid KeyColumns specification in property {propRef}: # of columns ({fkNames.Length}) " +
            $" does not match # of columns ({toKeyMembers.Count}) in target primary key.");
          return false;
        }
      }
      // build members
      for (var i = 0; i < toKeyMembers.Count; i++) {
        var targetMember = toKeyMembers[i].Member;
        string fkMemberName = (fkNames == null) ? refMember.MemberName + "_" + targetMember.MemberName : fkMemberName = fkNames[i];
        var memberType = targetMember.DataType;
        //If reference is nullable, then force member to be nullable too - and flip c# type to nullable
        if (nullable && (memberType.IsValueType || memberType.IsEnum)) {
          //CLR type is not nullable - flip it to nullable
          memberType = ReflectionHelper.GetNullable(memberType);
        }
        var fkMember = key.Entity.GetMember(fkMemberName);
        // if member exists, it is declared explicitly, or maybe it is part of another FK
        if (fkMember != null) {
          // check it matches the type
          if (fkMember.DataType != memberType) {
            Log.LogError($"Property {propRef}: underlying foreign key column '{fkMemberName}' already exists - " +
              $"it is declared explicitly (or is a part of another key) and its type {fkMember.DataType} does not match foreign key column type {memberType.Name}.");
            return false;
          }
        } else {
          //create new column member
          fkMember = new EntityMemberInfo(key.Entity, EntityMemberKind.Column, fkMemberName, memberType);
          fkMember.ExplicitDbTypeSpec = targetMember.ExplicitDbTypeSpec;
        }
        fkMember.Flags |= EntityMemberFlags.ForeignKey;
        if (targetMember.Size > 0)
          fkMember.Size = targetMember.Size;
        if (targetMember.Flags.IsSet(EntityMemberFlags.AutoValue)) {
          fkMember.Flags |= EntityMemberFlags.AutoValue;
        }
        fkMember.ForeignKeyOwner = refMember;
        if (nullable)
          fkMember.Flags |= EntityMemberFlags.Nullable;
        if (isPk)
          fkMember.Flags |= EntityMemberFlags.PrimaryKey;
        //copy old names
        if (key.OwnerMember.OldNames != null)
          fkMember.OldNames = key.OwnerMember.OldNames.Select(n => n + "_" + targetMember.MemberName).ToArray();
        key.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(fkMember, false));
        key.BuildStatus = KeyBuildStatus.ExpandedKeyMembersDone;
      }//foreach targetMember
      return true;
    }


  } //class


  // =============================== Helper =======================================

  internal static class KeyBuilderHelper {

    public static string GetSafeKeyRef(this EntityKeyInfo key) {
      return $"{key.Entity.Name}/{key.KeyType}";
    }
  }
}
