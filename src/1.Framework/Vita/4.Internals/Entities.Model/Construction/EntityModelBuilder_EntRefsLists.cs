using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Vita.Entities.Utilities;
using Vita.Entities.Logging;

namespace Vita.Entities.Model.Construction {
  partial class EntityModelBuilder {

    private void ProcessEntityRefMembers() {
      foreach(var ent in this.Model.Entities)
        foreach(var member in ent.Members) 
          if(member.Kind == EntityMemberKind.EntityRef)
            ProcessEntityRefMember(member); 
    }

    private void ProcessEntityRefMember(EntityMemberInfo member) {
      // Check if EntityRef attr is present
      var entRefAttr = member.Attributes.OfType<EntityRefAttribute>().FirstOrDefault();

      var entity = member.Entity;
      var propName = entity.EntityType.Name + "." + member.MemberName;
      var targetType = member.DataType;
      var targetEntity = this.Model.GetEntityInfo(targetType);
      if(targetEntity == null) {
        Log.LogError($"Reference property {propName}: target entity {targetType} not found or not registered.");
        return;
      }

      //find target key; usually it is PK on target entity, but MS SQL allows linking to any unique key
      // EntityRef attr might specify target unique key
      var targetKey = targetEntity.PrimaryKey;
      var targetUniqueIndexAlias = entRefAttr?.TargetUniqueIndexAlias;
      if(!string.IsNullOrEmpty(targetUniqueIndexAlias)) {
        targetKey = FindCreateKeyByAlias(targetEntity, targetUniqueIndexAlias);
        if(targetKey == null) {
          Log.LogError($"Property {propName}: Invalid target index alias '{targetUniqueIndexAlias}' in EntityRef attrribute, " + 
            $" index not found on target entity {targetEntity.EntityType}.");
          return;
        }
        if(!targetKey.KeyType.IsSet(KeyType.Unique)) {
          Log.LogError($"Property {propName}: Invalid target Index in EntityRef attrribute; Index {targetUniqueIndexAlias} is not Unique.");
          return;
        }
      }
      //Create foreign key
      var fk = new EntityKeyInfo(entity, KeyType.ForeignKey, sourceRefAttr: entRefAttr);
      fk.IsCopyOf = targetKey;
      fk.KeyMembers.Add(new EntityKeyMemberInfo(member, false));
      var refInfo = member.ReferenceInfo = new EntityReferenceInfo(member, fk, targetKey);
      // if there's EntitRef attr, apply its props if specified
      if (entRefAttr != null) {
        if(!string.IsNullOrEmpty(entRefAttr.ForeignKeyName))
          refInfo.FromKey.Name = entRefAttr.ForeignKeyName;
        if(!string.IsNullOrWhiteSpace(entRefAttr.KeyColumns))
          refInfo.ForeignKeyColumns = entRefAttr.KeyColumns;
      }
      if(targetEntity.Flags.IsSet(EntityFlags.HasIdentity))
        entity.Flags |= EntityFlags.ReferencesIdentity; 
    }

    private void ProcessEntityListMembers() {
      foreach(var ent in this.Model.Entities)
        foreach(var member in ent.Members)
          if(member.Kind == EntityMemberKind.EntityList) {
            var m2m = member.Attributes.OfType<ManyToManyAttribute>().FirstOrDefault();
            if(m2m != null)
              ProcessManyToManyListMember(member, m2m);
            else
              ProcessOneToManyListMember(member);

          }
    }//method

    private void ProcessManyToManyListMember(EntityMemberInfo member, ManyToManyAttribute attr) {
      var linkEntityType = attr.LinkEntity;
      var listInfo = member.ChildListInfo = new ChildEntityListInfo(member);
      listInfo.RelationType = EntityRelationType.ManyToMany;
      listInfo.LinkEntity = this.Model.GetEntityInfo(linkEntityType, true);
      listInfo.ParentRefMember = listInfo.LinkEntity.FindEntityRefMember(attr.ThisEntityRef, member.Entity.EntityType, member, this.Log);
      if(listInfo.ParentRefMember == null) {
        this.Log.LogError($"Many-to-many setup error: back reference to entity {member.Entity.EntityType} not found in link entity {linkEntityType}.");
        return;
      }
      listInfo.ParentRefMember.ReferenceInfo.TargetListMember = member;
      var targetEntType = member.DataType.GetGenericArguments()[0];
      listInfo.OtherEntityRefMember = listInfo.LinkEntity.FindEntityRefMember(attr.OtherEntityRef, targetEntType, member, this.Log);
      if(listInfo.OtherEntityRefMember != null)
        listInfo.TargetEntity = this.Model.GetEntityInfo(listInfo.OtherEntityRefMember.DataType, true);
    }

    private void ProcessOneToManyListMember(EntityMemberInfo member) {
      var listInfo = member.ChildListInfo = new ChildEntityListInfo(member);
      listInfo.RelationType = EntityRelationType.ManyToOne;
      var targetType = member.DataType.GetGenericArguments()[0];
      listInfo.TargetEntity = this.Model.GetEntityInfo(targetType, true);
      var oneToManyAttr = member.Attributes.OfType<OneToManyAttribute>().FirstOrDefault();
      var thisEntRefName = oneToManyAttr?.ThisEntityRef;
      if(!string.IsNullOrEmpty(thisEntRefName)) {
        var fkMember = listInfo.TargetEntity.GetMember(thisEntRefName);
        if(fkMember == null) {
          Log.LogError($"Entity list member {member.MemberName}: could not find property '{thisEntRefName}' in target entity. ");
          return;
        }
        oneToManyAttr.ThisEntityRef = fkMember.MemberName;
        listInfo.ParentRefMember = fkMember;
      } else {
        // no explicit attr
        listInfo.ParentRefMember = listInfo.TargetEntity.FindEntityRefMember(null, member.Entity.EntityType, member, this.Log);
      }
      //Check that reference is found
      if (listInfo.ParentRefMember == null)
        Log.LogError($"EntityList member {member.MemberName}: could not find reference property in target entity. ");
      else
        //Set back reference to list from ref member
        listInfo.ParentRefMember.ReferenceInfo.TargetListMember = member;
      //Filter - not implemented in .NET core version 
      /*
      if(oneToManyAttr != null && !string.IsNullOrWhiteSpace(oneToManyAttr.Filter))
        listInfo.Filter = ParseFilter(oneToManyAttr.Filter, listInfo.TargetEntity, this.Log);
        */
    } //method

    public static EntityFilter ParseFilter(string listFilter, EntityInfo entity, ILog log) {
      //Safely parse template - parse throws exc
      StringTemplate template;
      try {
        template = StringTemplate.Parse(listFilter);
      } catch(Exception ex) {
        log.LogError($"{entity.Name}, error in list filter: {ex.Message}");
        return null;
      }
      // map names to members
      var members = new List<EntityMemberInfo>(); 
      foreach(var name in template.ArgNames) {
        var member = entity.FindMemberOrColumn(name);
        if(member == null)
          log.LogError($"Entity {entity.EntityType}, error in filter expression, member/column {name} not found. ");
        else
          members.Add(member); 
      }
      return new EntityFilter() { Template = template, Members = members };
    }

  } //class
}
