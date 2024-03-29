﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {

  //  !!  See EntityKeysBuilder_ReadMe.md for more info about this class 

  internal partial class EntityKeyBuilder {
    EntityModelBuilder _modelBuilder;
    ILog _log; 
    List<EntityKeyInfo> _allKeys;

    internal EntityKeyBuilder(EntityModelBuilder modelBuilder) {
      _modelBuilder = modelBuilder;
      _log = modelBuilder.Log;
    }

    internal void BuildKeys() {
      _allKeys = _modelBuilder.Model.Entities.SelectMany(e => e.Keys).ToList();
      // step 1 - build key.KeyMembers lists, possibly with unassigned KeyMember.Member refs, 
      //           from either OwnerMember (keys on Members/Properties), or KeySpec (keys on entity)
      BuildInitialKeyMembersLists();
      _modelBuilder.CheckErrors();

      // Step 2 - run main loop, assigining target Member field in KeyMemberInfo, and expandig (KeyMembers into 
      //        KeyMembersExpanded)
      RunMembersAssignExpandLoop();
      _modelBuilder.CheckErrors();

      // check all expanded
      var nonExpanded = _allKeys.Where(key => key.MembersStatus < KeyMembersStatus.Expanded).ToList(); 
      if (nonExpanded.Count > 0) {
        var strKeys = string.Join("; ", nonExpanded.Select(key => key.GetKeyRefForError()));
        _log.LogError($"Key builder failed to expand the following keys: {strKeys}");
        _modelBuilder.CheckErrors(); 
      }

      // Step 3 - fill and expand Include members in Index keys
      BuildExpandTheIncludeMembers();

      BuildKeyNamesFilters();

    } // method

    // Filling initial key.KeyMembers lists; either from OwnerMember (single KeyMember), or from KeySpec
    //  (string, list of members as parameter of attribute, like PrimaryKey("CustId,Id")
    private void BuildInitialKeyMembersLists() {
      foreach (var key in _allKeys) {
        if (!string.IsNullOrEmpty(key.KeyMembersSpec)) {
          BuildKeyMembersFromKeySpec(key);
        } else if (key.OwnerMember != null) {
          BuildKeyMembersForKeyOnEntityMember(key);
        } else {
          var keyRef = key.GetKeyRefForError();
          // should never happen, this case should be caught earlier
          _log.LogError($"Key {keyRef} has no OwnerMember and no MembersSpec.");
          continue; 
        }
      } //foreach
    }

    // process keys on members; these keys have a single member; some of them can be immediately 
    // expanded (when OwnerMember is a simple column)
    private void BuildKeyMembersForKeyOnEntityMember(EntityKeyInfo key) {
      var keyMember = new EntityKeyMemberInfo(key.OwnerMember);
      key.KeyMembers.Add(keyMember);
      key.MembersStatus = KeyMembersStatus.Listed;
      // if it is a simple column, we can expand it right now
      if (keyMember.Member.Kind == EntityMemberKind.Column) {
        key.KeyMembersExpanded.Add(keyMember);
        key.MembersStatus = KeyMembersStatus.Expanded;
      }
    }

    public void BuildKeyMembersFromKeySpec(EntityKeyInfo key) {
      var ent = key.Entity;
      var allowAscDesc = key.KeyType.IsSet(KeyType.Index | KeyType.PrimaryKey);
      var spec = key.KeyMembersSpec;
      var success = true;
      var segments = StringHelper.SplitNames(key.KeyMembersSpec);
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
        var member = ent.FindMemberOrColumn(memberName); //might be null 
        key.KeyMembers.Add(new EntityKeyMemberInfo(memberName, member, desc));
      }//foreach segm
      //set build status
      if (!success)
        return;
      key.MembersStatus = (key.KeyMembers.All(km => km.Member != null))  ? KeyMembersStatus.Assigned: KeyMembersStatus.Listed;
    } // method


    private bool RunMembersAssignExpandLoop() {
      var repeatCount = 10;
      var workList = _allKeys.Where(key => key.MembersStatus < KeyMembersStatus.Expanded).ToList();
      // run loop several times;
      // keys are dependent on each other, so they sometimes cannot be completed/expanded in one pass
      //   1. try process FKs, this can adds columns mapped from target FK
      //   2. try process other keys (including PKs); the PKs may have members that are entity references (FKs), that's why
      //     they are not expanded until late.
      for (int i = 0; i < repeatCount; i++) {
        if (workList.Count == 0)
          return true; // we are done, success

        AssignExpandMembers(workList);
        // refresh work list
        workList = workList.Where(key => key.MembersStatus < KeyMembersStatus.Expanded).ToList();
      }
      // if we are not done after multiple iterations, it is an error - fatal error, something is broken in this algorithm
      ReportLoopFailed(workList);
      return false;
    }

    private void AssignExpandMembers(IList<EntityKeyInfo> keys) {
      foreach(var key in keys) {
        if (key.KeyType.IsSet(KeyType.ForeignKey)) {
          // the target PK key must be expanded (all key columns listed), to make it possible to expand the FK
          var toKey = key.OwnerMember.ReferenceInfo.ToKey;
          if (toKey.IsExpanded())
            ExpandForeignKeyMembers(key);
        } else {
          TryAssignKeyMemberRefs(key);
          TryExpandKeyMembers(key);
        }
      } //foreach key
    } //method

    private void TryAssignKeyMemberRefs(EntityKeyInfo key) {
      // Try assign all missing keyMember.Member refs
      var allAssigned = true; //assume
      if (key.MembersStatus == KeyMembersStatus.Listed) {
        foreach (var km in key.KeyMembers) {
          if (km.Member == null)
            km.Member = key.Entity.FindMemberOrColumn(km.MemberName);
          allAssigned &= km.Member != null;
        }
        if (allAssigned)
          key.MembersStatus = KeyMembersStatus.Assigned;
      }
    }

    private void TryExpandKeyMembers(EntityKeyInfo key) {
      // check if we can expand all members
      if (key.MembersStatus != KeyMembersStatus.Assigned)
        return;
      foreach(var km in key.KeyMembers) {
        if(!TryExpandMember(km, key.KeyMembersExpanded)) {
          key.KeyMembersExpanded.Clear();
          return; 
        }
      } // foreach
      key.MembersStatus = KeyMembersStatus.Expanded;
    } //method

    private bool TryExpandMember(EntityKeyMemberInfo keyMember, List<EntityKeyMemberInfo> toMembers) {
      var member = keyMember.Member;
      var kind = member.Kind;
      switch (kind) {
        case EntityMemberKind.Column:
          toMembers.Add(keyMember);
          return true;
        case EntityMemberKind.EntityRef:
          var fromKey = member.ReferenceInfo.FromKey;
          if (fromKey.MembersStatus < KeyMembersStatus.Expanded)
            return false; // not success 
                    // good so far, add expanded members
          toMembers.AddRange(fromKey.KeyMembersExpanded);
          return true;
        default:
          _log.LogError(
            @$"FATAL: Invalid kind {kind} for member {member.MemberName} pushed for expansion. ");
          return false;
      }// switch
    }

    //FK expansion is a special case - we expand members from target expanded members (of target PrimaryKey)
    private bool ExpandForeignKeyMembers(EntityKeyInfo key) {
      var refMember = key.OwnerMember;
      var refInfo = refMember.ReferenceInfo;
      if (!refInfo.ToKey.IsExpanded())
        return false; 
      var nullable = refMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var isPk = refMember.Flags.IsSet(EntityMemberFlags.PrimaryKey);
      // check/expand ToKey
      var toKeyMembers = refInfo.ToKey.KeyMembersExpanded;
      var propRef = refMember.Entity.EntityType.Name + "." + refMember.MemberName;
      // Check if we have explicitly specified names in ForeignKey attribute
      string[] fkNames = null;
      if (!string.IsNullOrEmpty(refInfo.ForeignKeyColumns)) {
        fkNames = refInfo.ForeignKeyColumns.SplitNames(',', ';');
        if (fkNames.Length != toKeyMembers.Count) {
          
          _log.LogError($"Invalid KeyColumns specification in property {propRef}: # of columns ({fkNames.Length}) " +
            $" does not match # of columns ({toKeyMembers.Count}) in the target key.");
          return false;
        }
      }
      // build members
      for (var i = 0; i < toKeyMembers.Count; i++) {
        var targetMember = toKeyMembers[i].Member;
        string fkMemberName = (fkNames == null) ? refMember.MemberName + "_" + targetMember.MemberName : fkMemberName = fkNames[i];
        var targetMemberType = targetMember.DataType;
        //If reference is nullable, then force member to be nullable too - and flip c# type to nullable
        if (nullable && targetMemberType.IsValueType) {
          //CLR type is not nullable - flip it to nullable
          targetMemberType = ReflectionHelper.GetNullable(targetMemberType);
        }
        var fkMember = key.Entity.FindMemberOrColumn(fkMemberName);
        // if member exists, it is declared explicitly, or maybe it is part of another FK
        if (fkMember != null) {
          // check it matches the type
          if (fkMember.DataType.UnwrapNullable() != targetMemberType.UnwrapNullable()) {
            _log.LogError($"Property {propRef} useed by member '{fkMemberName}' " +
              $" type {fkMember.DataType} does not match target key column type {targetMember.FullName}.");
            return false;
          }
        } else {
          //create new column member
          fkMember = new EntityMemberInfo(key.Entity, EntityMemberKind.Column, fkMemberName, targetMemberType);
          fkMember.ExplicitDbTypeSpec = targetMember.ExplicitDbTypeSpec;
        }
        fkMember.Flags |= EntityMemberFlags.ForeignKey;
        if (targetMember.Size > 0)
          fkMember.Size = targetMember.Size;
        if (targetMember.Flags.IsSet(EntityMemberFlags.AutoValue)) {
          fkMember.Flags |= EntityMemberFlags.AutoValue;
        }
        if (nullable)
          fkMember.Flags |= EntityMemberFlags.Nullable;
        if (isPk)
          fkMember.Flags |= EntityMemberFlags.PrimaryKey;

        if (refInfo.ToKey.KeyMembers.Any(km => km.Member.Flags.IsSet(EntityMemberFlags.Identity))) {
          fkMember.Flags |= EntityMemberFlags.IdentityForeignKey;
          fkMember.OwnerIdSourceRefMember = refMember; 

        }
        //copy old names
        if (key.OwnerMember.OldNames != null)
          fkMember.OldNames = key.OwnerMember.OldNames.Select(n => n + "_" + targetMember.MemberName).ToArray();
        key.KeyMembersExpanded.Add(new EntityKeyMemberInfo(fkMember, false));
        key.MembersStatus = KeyMembersStatus.Expanded;
      }//foreach targetMember
      return true;
    }

    private void BuildExpandTheIncludeMembers() {
      // only index keys have include-members spec
      var keys = _allKeys.Where(k => k.KeyType.IsSet(KeyType.Index) &&
                         !string.IsNullOrWhiteSpace(k.IncludeMembersSpec))
                   .ToList();
      foreach (var key in keys) {
        var keyRef = key.GetFullRef();
        var names = key.IncludeMembersSpec.Split(';', ',').Select(m => m.Trim()).ToList();
        foreach (var memberName in names) {
          if (memberName == string.Empty) {
            _log.LogError($"Invalid Include spec on key '{keyRef}', empty member name (double comma?).");
            break; //foreach memberName
          }
          var member = key.Entity.FindMemberOrColumn(memberName);
          if (member == null) {
            _log.LogError($"Invalid Include list in Index '{keyRef}'; member '{memberName}' not found.");
            continue; //next
          }

          key.IncludeMembers.Add(member);
          switch (member.Kind) {
            case EntityMemberKind.Column:
              key.IncludeMembersExpanded.Add(member);
              break;
            case EntityMemberKind.EntityRef:
              var fkCols = member.ReferenceInfo.FromKey.KeyMembersExpanded.Select(km => km.Member);
              key.IncludeMembersExpanded.AddRange(fkCols);
              break;
            default:
              _log.LogError($"Invalid Include member '{memberName}', in Index '{keyRef}': " +
                             $"member of this kind may not be used in include list.");
              break;
          }
        } // foreach
      }// foreach key
    } // method

    private void BuildKeyNamesFilters() {
      foreach (var ent in _modelBuilder.Model.Entities)
        foreach (var key in ent.Keys) {
          if (string.IsNullOrWhiteSpace(key.ExplicitDbKeyName))
            key.Name = key.ConstructKeyName();
          else
            key.Name = key.ExplicitDbKeyName;
          // Filter
          if (!string.IsNullOrWhiteSpace(key.FilterSpec))
            key.ParsedFilterTemplate = ParseFilter(key.FilterSpec, key.Entity);
        }
    }

    private EntityFilterTemplate ParseFilter(string listFilter, EntityInfo entity) {
      //Safely parse template - parse throws exc
      StringTemplate template;
      try {
        template = StringTemplate.Parse(listFilter);
      } catch (Exception ex) {
        _log.LogError($"{entity.Name}, error in list filter: {ex.Message}");
        return null;
      }
      // map names to members
      var members = new List<EntityMemberInfo>();
      foreach (var name in template.ArgNames) {
        var member = entity.FindMemberOrColumn(name);
        if (member == null)
          _log.LogError($"Entity {entity.EntityType}, error in filter expression, member/column {name} not found. ");
        else
          members.Add(member);
      }
      return new EntityFilterTemplate() { Template = template, Members = members };
    }

    private void ReportLoopFailed(IList<EntityKeyInfo> workList) {
      var keyListStr = string.Join("\r\n", workList.Select(km => km.GetKeyRefForError()));
      _log.LogError(
        @$"FATAL: Key builder process could not complete, remaining key count: {workList.Count}. Keys:\r\n {keyListStr}");
      var notFound = workList.SelectMany(
           key => key.KeyMembers.Where(km => km.Member == null).Select(km => km.MemberName)
           ).ToList();
      var notFoundStr = string.Join("\r\n  ", notFound);
      if (notFound.Count > 0) {
        _log.LogError($"The following properties/members could not be found:\r\n {notFoundStr}");
      }
    }

  } //class
}
