using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Model;


namespace Vita.Entities.Runtime {

  internal class PropertyBoundListManyToMany<TEntity> : PropertyBoundListBase<TEntity> where TEntity : class {
    //Lookup table for link records. (Entity) => LinkRecord
    public Dictionary<EntityRecord, EntityRecord> LinkRecordsLookup = new Dictionary<EntityRecord, EntityRecord>();

    public PropertyBoundListManyToMany(EntityRecord ownerRecord, EntityMemberInfo ownerMember) : base(ownerRecord, ownerMember) { 
    }

    public override void Notify(BoundListEventType eventType) {
      var session = OwnerRecord.Session; 
      switch (eventType) {
        case BoundListEventType.SavingChanges:
          if (!IsLoaded || !Modified) return;
          UpdateLinkRecords();
          break;
        case BoundListEventType.SavedChanges:
          // We effectively force reload of each m2m list if a single link entity was modified/added/deleted
          // - even if all changes went thru this list, and it is up to date.
          // TODO: maybe add a flag to EntitySession to on/off this force reload of all lists
          var linkEnt = OwnerMember.ChildListInfo.LinkEntity;
          if (session.RecordsChanged.Any(r => r.EntityInfo == linkEnt))
            Entities = null; //to force reload
          Modified = false;
          break;
        case BoundListEventType.CanceledChanges:
          LinkRecordsLookup.Clear(); 
          Modified = false; 
          Entities = null; //to force reload
          break; 
      }//switch
    }//method

    public override void LoadList() { 
      Modified = false;
      LinkRecordsLookup.Clear();
      var status = OwnerRecord.Status;
      if (status == EntityStatus.Fantom || status == EntityStatus.New) {
        Entities = new List<IEntityRecordContainer>();
        return;
      }

      var session = OwnerRecord.Session;
      var listInfo = OwnerMember.ChildListInfo;
      var cmdInfo = listInfo.GetSelectDirectChildRecordsCommand();
      var cmd = new EntityCommand(cmdInfo, listInfo.LinkEntity, OwnerRecord.PrimaryKey.Values);
      var linkEntList = (IList) session.ExecuteCommand(cmd); 
      /* Not needed - previous select cmd should have include for target records
      // Preload target entity records, in addition to link records.
      // These records will be cached in session, so when we try to read the target entity below it will be already loaded.
      // Preload is worth doing only if we have 2 or more link records.
      // Also, we do it only if record is NOT coming from full set cache; if it is, then target records should be in cache too,
      // so it does not make sense to cache them again in the current session.
      if (linkEntList.Count > 1 && OwnerRecord.EntityInfo.CachingType != EntityCachingType.FullSet) {
        cmdInfo = listInfo.SelectByKeyValueCommandM2M;
        cmd = new LinqCommand(cmdInfo, listInfo.TargetEntity, OwnerRecord.PrimaryKey.Values);
        session.ExecuteSelect(cmd);
      }
      */ 

      // Build LinkRecordsLookup and result entity list
      var targetEntList = new List<IEntityRecordContainer>();
      for (int i = 0; i < linkEntList.Count; i++) {
        var linkEnt = (IEntityRecordContainer)linkEntList[i];
        var linkRec = linkEnt.Record; 
        var targetEnt = (IEntityRecordContainer)linkRec.GetValue(listInfo.OtherEntityRefMember);
        targetEntList.Add(targetEnt);
        LinkRecordsLookup[targetEnt.Record] = linkRec;
      }
      Entities = targetEntList;
    }

    //We do not create/modify link records when app code manipulates the list. Instead, we wait until Session.SaveChanges 
    // and then adjust link records for the entire list
    private void UpdateLinkRecords() {
      var persistentOrderMember = OwnerMember.ChildListInfo.PersistentOrderMember;
      var toDeleteLinks = new HashSet<EntityRecord>(LinkRecordsLookup.Values); // initialize with all link records
      for (int i = 0; i < Entities.Count; i++) {
        var ent = Entities[i];
        EntityRecord linkRec;
        if (LinkRecordsLookup.TryGetValue(ent.Record, out linkRec)) {
          toDeleteLinks.Remove(linkRec);
        } else {
          linkRec = CreateLinkRecord(ent);
          LinkRecordsLookup[ent.Record] = linkRec;
        }
        //Handle persistent ordering
        if (IsOrdered)
          (linkRec).SetValueDirect(persistentOrderMember, i + 1, setModified: true); // Persistent order is 1-based
      }
      // Now handle deleted links - whatever is left in unusedLinks
      var session = OwnerRecord.Session;
      foreach (var delLink in toDeleteLinks)
        session.DeleteRecord(delLink);
      //to be completed
    }

    private EntityRecord CreateLinkRecord(IEntityRecordContainer targetEntity) {
      var listInfo = OwnerMember.ChildListInfo; 
      var linkRec = OwnerRecord.Session.NewRecord(listInfo.LinkEntity);
      linkRec.SetValue(listInfo.ParentRefMember, OwnerRecord.EntityInstance);
      linkRec.SetValue(listInfo.OtherEntityRefMember, targetEntity);
      return linkRec;
    }

    public override void Init(IList<IEntityRecordContainer> entities, IList<IEntityRecordContainer> linkEntities = null) { 
      base.Entities = entities;
      //Fill up link records lookup
      LinkRecordsLookup.Clear();
      var linkToTargetMember = this.OwnerMember.ChildListInfo.OtherEntityRefMember;
      foreach(var linkEnt in linkEntities) {
        var linkRec =  EntityHelper.GetRecord(linkEnt);
        var targetEnt = linkRec.GetValue(linkToTargetMember) as EntityBase;
        LinkRecordsLookup[targetEnt.Record] = linkRec; 
      }
      base.Modified = false;
    }

  }//class
}
