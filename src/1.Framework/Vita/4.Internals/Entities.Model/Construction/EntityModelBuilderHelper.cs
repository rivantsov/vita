using System;
using System.Collections.Generic;
using System.Linq;

using Vita.Entities.Utilities;
using Vita.Entities.Logging;
using System.Reflection;

namespace Vita.Entities.Model.Construction {

  public static class EntityModelBuilderHelper {

    // new stuff for entity key fix 
    // returns keys that this key 'depends on', so that these other keys must be expanded first. 
    // will be used in topological sorting (SCC)
    public static IList<EntityKeyInfo> GetDependsOnKeysForExpansion(this EntityKeyInfo key) {
      var refKeyMembers = key.KeyMembers.Where(km => km.Member.Kind == EntityMemberKind.EntityRef).ToList(); 
      if (refKeyMembers.Count == 0)
        return EntityKeyInfo.EmptyList;
      // long path
      var list = new List<EntityKeyInfo>();
      foreach(var km in refKeyMembers) 
        list.Add(km.Member.ReferenceInfo.ToKey);
      return list; 
    }


    // end new stuff


    public static string GetAttributeName(this Attribute attr) {
      const string suffix = "Attribute";
      var name = attr.GetType().Name;
      if(name.EndsWith(suffix))
        name = name.Substring(0, name.Length - suffix.Length);
      return name;
    }

    // these are not key names in db, we assign DbKey.Name in DbModelBuilder using some special code
    public static string ConstructKeyName(this EntityKeyInfo key) {
      Util.Check(key.IsExpanded(), "KeyMembers list must be expanded, cannot construct name. Entity: {0}, keytype: {1}", 
          key.Entity.Name, key.KeyType);
      var entity = key.Entity;
      var tName = entity.TableName ?? entity.Name;
      var prefix = GetKeyNamePrefix(key.KeyType);
      if (key.KeyType.IsSet(KeyType.PrimaryKey)) {
        return prefix + tName;
      } else if (key.KeyType.IsSet(KeyType.ForeignKey)) {
        var target = key.OwnerRefMember.ReferenceInfo.ToKey.Entity;
        return prefix + tName + "_" + (target.TableName ?? target.Name);
      } else {
        var members = key.GetMemberNamesAsString();
        string memberNames = string.Join(string.Empty, members).Replace("_", string.Empty); //remove underscores in names 
        return prefix + tName + "_" + memberNames;
      }
    }

    public static string GetKeyNamePrefix(KeyType keyType) {
      if(keyType.IsSet(KeyType.PrimaryKey))
        return "PK_";
      if(keyType.IsSet(KeyType.ForeignKey))
        return "FK_";
      var prefix = "IX";
      if(keyType.IsSet(KeyType.Clustered))
        prefix += "C";
      else if(keyType.IsSet(KeyType.Unique))
        prefix += "U";
      prefix += "_";
      return prefix; 
    }



    public static int GetDefaultMemberSize(this EntityMemberInfo member) {
      var type = member.DataType;
      // Set default for string size, might be changed later by attributes
      if(type == typeof(string))
        return member.Entity.Area.App.DefaultStringLength;
      else if(type == typeof(char))
        return 1;
      else if(type == typeof(byte[]) || type == typeof(Binary))
        return 128;
      return 0;
    }

    public static bool TryGetEntityTypeFromList(Type listType, out Type entityType) {
      entityType = null;
      if(!listType.IsGenericType)
        return false;
      var genType = listType.GetGenericTypeDefinition();
      if(!typeof(IList<>).IsAssignableFrom(genType))
        return false;
      entityType = listType.GetGenericArguments()[0];
      return true;
    }


    public static bool TryParseKeySpec(this EntityInfo entity, string keySpec, ILog log, out List<EntityKeyMemberInfo> parsedKeyMembers, 
          bool ordered, EntityInfo specHolder = null) {
      specHolder = specHolder ?? entity; 
      var specs = StringHelper.SplitNames(keySpec);
      parsedKeyMembers = new List<EntityKeyMemberInfo>();
      foreach(var spec in specs) {
        bool desc = false;
        string[] parts;
        if(ordered) {
          parts = StringHelper.SplitNames(spec, ':');
          if(parts.Length > 2) {
            log.LogError($"Invalid key/order spec '{keySpec}', entity {specHolder.EntityType}; only single ':' char is allowed.");
            return false; 
          }
          string strDesc = parts.Length == 1 ? "asc" : parts[1];
          switch(strDesc.ToLowerInvariant()) {
            case "":  case "asc": desc = false; break;
            case "desc": desc = true; break; 
            default:
              log.LogError($"Invalid key/order spec '{keySpec}', entity {specHolder.EntityType}. Expected ':asc' or ':desc' as direction specification.");
              return false; 
          }//switch
        }//if ordered
        else 
          parts = new string[] { spec, null };
        var member = entity.GetMember(parts[0]);
        if(member == null) {
          log.LogError($"Invalid key/order spec '{keySpec}', entity {specHolder.EntityType}. member {parts[0]} not found.");
          return false; 
        }
        parsedKeyMembers.Add(new EntityKeyMemberInfo(member, desc));
      }//foreach spec
      return true; 
    }


    public static EntityMemberInfo FindEntityRefMember(this EntityInfo inEntity, string memberNameToFind, Type typeToFind, EntityMemberInfo listMember, ILog log) {
      IList<EntityMemberInfo> refMembers;
      if (!string.IsNullOrEmpty(memberNameToFind)) {
        refMembers = inEntity.Members.FindAll(m => m.MemberName == memberNameToFind);
      } else
        refMembers = inEntity.Members.FindAll(m => m.DataType == typeToFind || 
          m.DataType.IsAssignableFrom(typeToFind) || typeToFind.IsAssignableFrom(m.DataType));
      if (refMembers.Count == 1)
        return refMembers[0];
      //Report error
      var listMemberDesc = listMember.Entity.FullName + "." + listMember.MemberName;
      if (refMembers.Count == 0)
        log.LogError($"EntityList member {listMemberDesc}: could not find matching foreign key member in target entity {inEntity.EntityType}. ");
      if (refMembers.Count > 1)
        log.LogError($"EntityList member {listMemberDesc}: more than one matching foreign key member in target entity {inEntity.EntityType}. ");
      return null;
    }

    public static List<EntityModelAttributeBase> SelectModelAttributes(this IList<Attribute> list) {
      return list.Where(a => a is EntityModelAttributeBase).Select(a => (EntityModelAttributeBase)a).ToList();
    }
    public static List<KeyAttribute> SelectKeyAttributes(this IList<Attribute> list) {
      return list.Where(a => a is KeyAttribute).Select(a => (KeyAttribute)a).ToList();
    }

    public static IList<KeyAttribute> GetKeyAttributes(this EntityInfo entity) {
      var list = entity.Attributes.SelectKeyAttributes();
      var mAttrs = entity.Members.SelectMany(m => m.Attributes.SelectKeyAttributes());
      list.AddRange(mAttrs);
      return list; 
    }

    public static void Apply(this EntityModelAttributeBase attr, EntityModelBuilder builder) {
      if(attr.HostMember == null)
        attr.ApplyOnEntity(builder);
      else
        attr.ApplyOnMember(builder); 
    }

    public static MethodInfo FindFunction(this EntityModule module, string functionName, Type containerType = null) {
      if (containerType != null) {
        return FindStaticMethod(containerType, functionName);
      }
      foreach(var type in module.CustomFunctionContainers) {
        var method = FindStaticMethod(type, functionName);
        if (method != null)
          return method; 
      }
      // try in module itself
      return FindStaticMethod(module.GetType(), functionName); 

    }

    private static MethodInfo FindStaticMethod(Type type, string methodName) {
      var bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
      var method = type.GetMethod(methodName, bFlags);
      return method; 
    }

    public static string GetMemberNamesAsString(this EntityKeyInfo key, string separator = "", bool removeUnderscore = false) {
      string result;
      if (key.ExpandedKeyMembers.Count == 1)
        result = key.ExpandedKeyMembers[0].Member.MemberName;
      else
        result = string.Join(separator, key.ExpandedKeyMembers.Select(m => m.Member.MemberName));
      if (removeUnderscore)
        result = result.Replace("_", string.Empty);
      return result;
    }



  } //class
}
