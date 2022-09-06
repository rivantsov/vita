using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {
  // key expansion methods
  public partial class EntityModelBuilder {

    private List<EntityKeyInfo> _keysInExpansion;


    // Some keys may contain members that are entity references;
    private void ExpandEntityKeyMembers() {
      _keysInExpansion = new List<EntityKeyInfo>(); 
      var allKeys = Model.Entities.SelectMany(e => e.Keys).ToList();
      foreach(var key in allKeys)
        if(!ExpandKey(key))
          return;
      _keysInExpansion.Clear(); 
      CheckErrors(); 
    }

    private void SetKeyNames() {
      foreach(var ent in Model.Entities)
        foreach(var key in ent.Keys)
          if(string.IsNullOrWhiteSpace(key.ExplicitDbKeyName))
            key.Name = key.ConstructKeyName();
          else
            key.Name = key.ExplicitDbKeyName; 

    }

    private bool ExpandKey(EntityKeyInfo key) {
      if(key.IsExpanded())
        return true;
      if(_keysInExpansion.Contains(key)) {
        Log.LogError($"Cannot expand key/index {key.GetFullRef()}, ran into key circular reference.");
        return false;
      }
      try {
        _keysInExpansion.Add(key);
        if(key.KeyType.IsSet(KeyType.ForeignKey))
          return ExpandForeignKey(key);
        ExpandRegularKey(key);
      } finally {
        _keysInExpansion.Remove(key);
      }
      key.HasIdentityMember = key.ExpandedKeyMembers.Any(m => m.Member.Flags.IsSet(EntityMemberFlags.Identity));
      return true;
    }

    // regular, non-foreign key
    private bool ExpandRegularKey(EntityKeyInfo key) {
      key.ExpandedKeyMembers.Clear();
      if(!ExpandRefMembers(key.KeyMembers.Select(km => km.Member).ToList()))
        return false;
      if(!ExpandRefMembers(key.IncludeMembers))
        return false;
      // Key members
      foreach(var km in key.KeyMembers) {
        var member = km.Member;
        if(member.Kind == EntityMemberKind.EntityRef)
          key.ExpandedKeyMembers.AddRange(member.ReferenceInfo.FromKey.ExpandedKeyMembers);
        else
          key.ExpandedKeyMembers.Add(km);
      } //foreach km
      // include members
      foreach(var member in key.IncludeMembers) {
        if(member.Kind == EntityMemberKind.EntityRef) {
          key.ExpandedIncludeMembers.AddRange(member.ReferenceInfo.FromKey.ExpandedKeyMembers.Select(km => km.Member));
        } else
          key.ExpandedIncludeMembers.Add(member);
      } //foreach km
      return true;
    }//method

    private bool ExpandRefMembers(IList<EntityMemberInfo> members) {
      var refMembers = members.Where(m => m.Kind == EntityMemberKind.EntityRef).ToList();
      foreach(var refM in refMembers) {
        var fromKey = refM.ReferenceInfo.FromKey;
        if(fromKey.IsExpanded())
          continue;
        if(!ExpandForeignKey(fromKey))
          return false;
      } //foreach member
      return true;

    }//method


    //FK expansion is a special case - we expand members from target expanded members (of target PrimaryKey)
    private bool ExpandForeignKey(EntityKeyInfo key) {
      var refMember = key.OwnerRefMember;
      var nullable = refMember.Flags.IsSet(EntityMemberFlags.Nullable);
      var isPk = refMember.Flags.IsSet(EntityMemberFlags.PrimaryKey);
      var refInfo = refMember.ReferenceInfo;
      // check/expand ToKey
      if(!refInfo.ToKey.IsExpanded())
        if(!ExpandKey(refInfo.ToKey))
          return false;
      var toKeyMembers = refInfo.ToKey.ExpandedKeyMembers;
      var propRef = refMember.Entity.EntityType.Name + "." + refMember.MemberName;
      // Check if we have explicitly specified names in ForeignKey attribute
      string[] fkNames = null;
      if(!string.IsNullOrEmpty(refInfo.ForeignKeyColumns)) {
        fkNames = refInfo.ForeignKeyColumns.SplitNames(',', ';');
        if(fkNames.Length != toKeyMembers.Count) {
          Log.LogError($"Invalid KeyColumns specification in property {propRef}: # of columns ({fkNames.Length}) " + 
            $" does not match # of columns ({toKeyMembers.Count}) in target primary key.");
          return false;
        }
      }
      // build members
      for(var i = 0; i < toKeyMembers.Count; i++) {
        var targetMember = toKeyMembers[i].Member;
        string fkMemberName = (fkNames == null) ? refMember.MemberName + "_" + targetMember.MemberName : fkMemberName = fkNames[i];
        var memberType = targetMember.DataType;
        //If reference is nullable, then force member to be nullable too - and flip c# type to nullable
        if(nullable && (memberType.IsValueType || memberType.IsEnum)) {
          //CLR type is not nullable - flip it to nullable
          memberType = ReflectionHelper.GetNullable(memberType);
        }
        var fkMember = key.Entity.GetMember(fkMemberName);
        // if member exists, it is declared explicitly, or maybe it is part of another FK
        if(fkMember != null) {
          // check it matches the type
          if(fkMember.DataType != memberType) {
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
        if(targetMember.Size > 0)
          fkMember.Size = targetMember.Size;
        if(targetMember.Flags.IsSet(EntityMemberFlags.AutoValue)) {
          fkMember.Flags |= EntityMemberFlags.AutoValue;
        }
        fkMember.ForeignKeyOwner = refMember;
        if(nullable)
          fkMember.Flags |= EntityMemberFlags.Nullable;
        if(isPk)
          fkMember.Flags |= EntityMemberFlags.PrimaryKey;
        //copy old names
        if(key.OwnerRefMember.OldNames != null)
          fkMember.OldNames = key.OwnerRefMember.OldNames.Select(n => n + "_" + targetMember.MemberName).ToArray();
        key.ExpandedKeyMembers.Add(new EntityKeyMemberInfo(fkMember, false));
      }//foreach targetMember
      return true;
    }


  }
}
