using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Model;


namespace Vita.Entities.Runtime {

  public class PropertyBoundListManyToOne<TEntity> : PropertyBoundListBase<TEntity> {

    public PropertyBoundListManyToOne(EntityRecord ownerRecord, EntityMemberInfo ownerMember) : base(ownerRecord, ownerMember) { }

    public override void Notify(BoundListEventType eventType) {
      var session = OwnerRecord.Session;
      switch (eventType) {
        case BoundListEventType.SavingChanges:
          if (OwnerRecord.Status == EntityStatus.Deleting) 
            return;
          if (IsLoaded && IsOrdered && Modified)
            AssignPersistentOrder();
          break;
        case BoundListEventType.SavedChanges:
          if (session.RecordsChanged.Any(r => r.EntityInfo == TargetEntity))
            Entities = null; //to force reload
          Modified = false;
          break;
        case BoundListEventType.CanceledChanges:
          Entities = null; 
          Modified = false; 
          break; 
      }//switch
    }//method

    public override void LoadList() { 
      Modified = false;
      var status = OwnerRecord.Status;
      if (status == EntityStatus.Fantom || status == EntityStatus.New) {
        Entities = new List<EntityBase>();
        return;
      }
      // var recs = OwnerRecord.Session.GetChildRecords(OwnerRecord, OwnerMember.ChildListInfo.ParentRefMember);
      var recs = OwnerRecord.Session.ExecuteSelect(OwnerMember.ChildListInfo.SelectDirectChildList, OwnerRecord.PrimaryKey.Values);
      Entities = recs.Select(r => r.EntityInstance).ToList();
    }

    private void AssignPersistentOrder() {
      var persMember = OwnerMember.ChildListInfo.PersistentOrderMember;
      var list = Entities;
      for (int i = 0; i < list.Count; i++) {
        var rec = EntityHelper.GetRecord(list[i]);
        var oldValue = rec.GetValueDirect(persMember);
        var newValue = (object)(i + 1); 
        if (oldValue != newValue)
          rec.SetValueDirect(persMember, newValue, setModified: true); // Persistent order is 1-based
      }
    }//method

    public override void Init(IList entities, IList linkEntities = null) {
      base.Entities = entities;
      base.Modified = false;
    }

  }//class
}
