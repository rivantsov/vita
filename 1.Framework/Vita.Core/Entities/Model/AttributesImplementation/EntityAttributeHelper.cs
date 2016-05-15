using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;

namespace Vita.Entities.Model.Construction {

  public static class EntityAttributeHelper {
    
    public static IList<EntityKeyMemberInfo> ParseMemberNames(EntityInfo entity, string names, bool ordered = false, Action<string> errorAction = null) {
      var specs = StringHelper.SplitNames(names);
      var mList = new List<EntityKeyMemberInfo>();
      foreach(var spec in specs) {
        bool desc = false;
        string[] parts;
        if(ordered) {
          parts = StringHelper.SplitNames(spec, ':');
          if(parts.Length > 2) {
            if(errorAction != null) errorAction(spec);
            continue; 
          }
          string strDesc = parts.Length == 1 ? "asc" : parts[1];
          switch(strDesc.ToLowerInvariant()) {
            case "":  case "asc": desc = false; break;
            case "desc": desc = true; break; 
            default:
              if(errorAction != null)
                errorAction(spec);
              continue; 
          }//switch
        }//if ordered
        else 
          parts = new string[] { spec, null };
        var member = entity.GetMember(parts[0]);
        if(member == null) {
          if(errorAction != null)
            errorAction(spec); 
        }
        mList.Add(new EntityKeyMemberInfo(member, desc));
      }//foreach spec
      return mList; 
    }


    public static string GetUniqueKeyName(this EntityInfo entity, string defaultName) {
      var cnt = 0;
      var result = defaultName;
      while (true) {
        if(! entity.Keys.Any(k => k.Name.Equals(result, StringComparison.InvariantCultureIgnoreCase)))
          return result;
        cnt++;
        result = defaultName + cnt; 
      }
    }

    public static EntityMemberInfo FindEntityRefMember(this EntityInfo inEntity, string memberNameToFind, Type typeToFind, EntityMemberInfo listMember, MemoryLog log) {
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
        log.Error("EntityList member {0}: could not find matching foreign key member in target entity {1}. ",
          listMemberDesc, inEntity.EntityType);
      if (refMembers.Count > 1)
        log.Error("EntityList member {0}: more than one matching foreign key member in target entity {1}. ",
          listMemberDesc, inEntity.EntityType);
      return null;
    }


  }

}
