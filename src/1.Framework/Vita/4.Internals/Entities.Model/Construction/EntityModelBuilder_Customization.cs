using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Logging;

namespace Vita.Entities.Model.Construction {

  public partial class EntityModelBuilder {

    private void ProcessAddedMembers() {
      foreach(var am in _customization.AddedMembers) {
        var ent = Model.GetEntityInfo(am.EntityType); 
        if (ent == null) {
          Log.LogError($"Invalid entity type for added member ({am.Name}), entity {am.EntityType} not registered.");
          continue; 
        }
        if(!TryGetMemberKind(ent, am.Name, am.DataType, out EntityMemberKind kind))
          continue;
        var member = new EntityMemberInfo(ent, kind, am.Name, am.DataType); //it will add member to entity.Members
        member.Attributes.AddRange(am.Attributes);
      }//foreach am
    }//method

    private void ProcessAddedIndexes() {
      foreach(var indInfo in _customization.AddedIndexes) {
        var ent = Model.GetEntityInfo(indInfo.EntityType);
        if(ent == null) {
          Log.LogError($"Invalid entity type for added index ({indInfo.EntityType}), entity not registered.");
          continue;
        }
        ent.Attributes.Add(indInfo.IndexAttribute); 
      }//foreach am
    }//method



    private void ProcessReplacedEntities() {
      if(_customization.Replacements.Count == 0)
        return; 
      // 1. Go thru replacements, find/create entity info for "new" entities, 
      //    and register this entity info under the key of replaced entity type
      foreach(var replInfo in _customization.Replacements) {
        var oldEntInfo = Model.GetEntityInfo(replInfo.ReplacedType);
        if(oldEntInfo == null) {
          Log.LogError($"Replacing entity {replInfo.ReplacedType}->{replInfo.NewType}; Error: type {replInfo.ReplacedType} is not a registered entity.");
          continue;
        }
        var newEntInfo = AddEntity(oldEntInfo.Module, replInfo.NewType);
        oldEntInfo.ReplacedBy = newEntInfo;
      }//foreach replInfo

      // 2. Trace replacedBy reference, find final replacing type and register entity info for final type under the "replaced type" key
      foreach(var entInfo in Model.Entities) {
        if(entInfo.ReplacedBy == null)
          continue;
        entInfo.ReplacedBy = GetFinalReplacement(entInfo);
        entInfo.ReplacedBy.ReplacesTypes.Add(entInfo.EntityType);
      }
      //rebuild internal entity lists
      Model.RebuildModelEntitySets();
    }


    //Note: returns entityInfo if there is no replacement
    private EntityInfo GetFinalReplacement(EntityInfo entityInfo) {
      var current = entityInfo;
      while(current.ReplacedBy != null)
        current = current.ReplacedBy;
      return current;
    }


  } //class
}
