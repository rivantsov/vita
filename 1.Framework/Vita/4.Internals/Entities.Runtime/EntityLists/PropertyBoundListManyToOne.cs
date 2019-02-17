using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data;
using Vita.Data.Linq;
using Vita.Entities.Model;


namespace Vita.Entities.Runtime {

  internal class PropertyBoundListManyToOne<TEntity> : PropertyBoundListBase<TEntity> where TEntity: class {

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
        Entities = new List<IEntityRecordContainer>();
        return;
      }
      var fromKey = OwnerMember.ChildListInfo.ParentRefMember.ReferenceInfo.FromKey;
      var orderBy = OwnerMember.ChildListInfo.OrderBy;
      var selectCmd = LinqCommandFactory.CreateSelectByKeyForListProperty(OwnerRecord.Session, OwnerMember.ChildListInfo,
                                             OwnerRecord.PrimaryKey.Values);
      var objEntList = (IList) OwnerRecord.Session.ExecuteLinqCommand(selectCmd);
      var recContList = new List<IEntityRecordContainer>();
      foreach (var ent in objEntList)
        recContList.Add((IEntityRecordContainer)ent);
      Entities = recContList;
    }

    private void AssignPersistentOrder() {
      var persMember = OwnerMember.ChildListInfo.PersistentOrderMember;
      var list = Entities;
      for (int i = 0; i < list.Count; i++) {
        var rec =  EntityHelper.GetRecord(list[i]);
        var oldValue = rec.GetValueDirect(persMember);
        var newValue = (object)(i + 1); 
        if (oldValue != newValue)
          rec.SetValueDirect(persMember, newValue, setModified: true); // Persistent order is 1-based
      }
    }//method

    public override void Init(IList<IEntityRecordContainer> entities, IList<IEntityRecordContainer> linkEntities = null) {
      base.Entities = entities;
      base.Modified = false;
    }

  }//class
}
