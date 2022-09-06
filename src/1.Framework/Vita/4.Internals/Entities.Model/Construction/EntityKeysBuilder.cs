using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Model.Construction {

  public partial class EntityKeysBuilder {
    EntityModelBuilder _modelBuilder;
    internal EntityModel Model => _modelBuilder.Model;
    ILog _log; 
    List<EntityKeyInfo> _allKeys;
    List<EntityKeyInfo> _pkFkKeys;

    internal EntityKeysBuilder(EntityModelBuilder modelBuilder) {
      _modelBuilder = modelBuilder;
      _log = _modelBuilder.Log; 
      _allKeys = Model.Entities.SelectMany(e => e.Keys).ToList();
    }

    internal void BuildKeys() {
      InitMemberSpecs(); 
    }

    

    internal void InitMemberSpecs() {
      // initialize member specs
      foreach(var key in _allKeys) {
        var kref = key.GetSafeKeyRef();
        // fast path for single-column PKs
        if (key.OwnerMember != null && key.KeyMemberListSpec == null) {
          key.KeyMemberListSpec = key.OwnerMember.MemberName;
          key.ParsedMemberSpecs = new[] { new MemberSpec { Name = key.OwnerMember.MemberName } };
          key.KeyMembers.Add(new EntityKeyMemberInfo(key.OwnerMember));
          continue;
        }
        // general case
        key.KeyMemberListSpec = key.KeyMemberListSpec ?? key.OwnerMember?.MemberName;
        if(string.IsNullOrEmpty(key.KeyMemberListSpec)) { //should never happen
          _log.LogError($"Fatal: missing or invalid key members list on entity {kref}.");
          continue;
        }
        key.ParsedMemberSpecs = TryParseKeySpec(key.KeyMemberListSpec, key.Entity, allowAscDecs: false);

      }      
    }

    private bool ResolveMemberList(EntityKeyInfo key) {

    }



    internal bool BuildMembers() {
      var pkFkList = allKeys.Where(key => key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      do {
        var newList = new List<EntityKeyInfo>();
        foreach (var key in pkFkList) 
          if (!TryExpandPrimaryOrForeignKey(key))
            newList.Add(key);
        
        // check if we are stuck, report error and exit if we are
        if (newList.Count == pkFkList.Count) { // no progress
          ReportErrorFailedToExpandKeys(pkFkList);
          return false; 
        }
        pkFkList = newList; 
      } while (pkFkList.Count > 0);
      _modelBuilder.CheckErrors();

      // process other keys
      var otherKeys = allKeys.Where(key => !key.KeyType.IsSet(KeyType.PrimaryKey | KeyType.ForeignKey)).ToList();
      foreach (var key in otherKeys)
        ExpandOtherKey(key);
      _modelBuilder.CheckErrors();
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
      }

    }


    private void ReportErrorFailedToExpandKeys(List<EntityKeyInfo> keys) {

    }

    // entity and specHolder are the same in most cases. Except for [OrderBy] attribute
    //  on list property. In this case 'entity' is target entity being ordered, and specHolder
    //  is entity that hosts the list property and attribute
    public List<MemberSpec> TryParseKeySpec(string keySpec, EntityInfo entity, bool allowAscDecs, 
                                            EntityInfo specHolder = null) {
      specHolder = specHolder ?? entity;
      var segments = StringHelper.SplitNames(keySpec);
      var specs = new List<MemberSpec>();
      foreach (var segm in segments) {
        bool desc = false;
        string[] parts;
        if (allowAscDecs) {
          parts = StringHelper.SplitNames(spec, ':');
          if (parts.Length > 2) {
            _log.LogError($"Invalid key/order spec '{keySpec}', entity {specHolder.EntityType}; only single ':' char is allowed.");
            return false;
          }
          string strDesc = parts.Length == 1 ? "asc" : parts[1];
          switch (strDesc.ToLowerInvariant()) {
            case "": case "asc": desc = false; break;
            case "desc": desc = true; break;
            default:
              log.LogError($"Invalid key/order spec '{keySpec}', entity {specHolder.EntityType}. Expected ':asc' or ':desc' as direction specification.");
              return false;
          }//switch
        }//if ordered
        else
          parts = new string[] { spec, null };
        var member = entity.GetMember(parts[0]);
        if (member == null) {
          log.LogError($"Invalid key/order spec '{keySpec}', entity {specHolder.EntityType}. member {parts[0]} not found.");
          return false;
        }
        parsedKeyMembers.Add(new EntityKeyMemberInfo(member, desc));
      }//foreach spec
      return true;
    }

  }

  // =============================== Helper =======================================

  internal static class KeyBuilderHelper {

    public static string GetSafeKeyRef(this EntityKeyInfo key) {
      return $"{key.Entity.Name}/{key.KeyType}";
    }
  }
}
