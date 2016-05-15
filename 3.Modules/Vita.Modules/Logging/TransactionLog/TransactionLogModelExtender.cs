using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Modules.Logging {

  public class TransactionLogModelExtender : IEntityModelExtender {
    internal List<UpdateStampColumnSpec> UpdateStampColumns = new List<UpdateStampColumnSpec>();
    //Programmatic way of adding tracking columns - holds a spec for extra column
    public class UpdateStampColumnSpec {
      public List<Type> Types = new List<Type>();
      public string CreateIdPropertyName;
      public string UpdateIdPropertyName;
    }

    public void AddUpdateStampColumns(IEnumerable<Type> toEntities, string createIdPropertyName = null, string updateIdPropertyName = null) {
      if(string.IsNullOrWhiteSpace(createIdPropertyName) && string.IsNullOrWhiteSpace(updateIdPropertyName))
        return; 
      UpdateStampColumns.Add(new UpdateStampColumnSpec() {
        Types = toEntities.ToList(), CreateIdPropertyName = createIdPropertyName, UpdateIdPropertyName = updateIdPropertyName
      });
    }

    public void Extend(EntityModel model) {
      if(model.ModelState != EntityModelState.EntitiesConstructed)
        return;
      //Add tracking properties (IDs of UserTransaction records) to all registered entities
      foreach(var spec in UpdateStampColumns) {
        foreach(var type in spec.Types) {
          var entInfo = model.GetEntityInfo(type);
          if(entInfo == null) {
            model.App.ActivationLog.Error("Failed to find entity info for type {0}", type);
            continue;
          }
          if(!string.IsNullOrEmpty(spec.CreateIdPropertyName)) {
            var newMember = new EntityMemberInfo(entInfo, MemberKind.Column, spec.CreateIdPropertyName, typeof(Guid));
            newMember.Attributes.Add(new TrackAttribute(TrackingActionType.Created));
          }
          if(!string.IsNullOrEmpty(spec.UpdateIdPropertyName)) {
            var newMember = new EntityMemberInfo(entInfo, MemberKind.Column, spec.UpdateIdPropertyName, typeof(Guid));
            newMember.Attributes.Add(new TrackAttribute(TrackingActionType.Updated));
          }
        }//foreach type
      }// foreach spec



    }//method

  }//class
}
