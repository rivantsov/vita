using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Caching;
using Vita.Entities.Model;


namespace Vita.Entities.Runtime {

  public class PropertyBoundListManyToMany<TEntity> : PropertyBoundListBase<TEntity> {
    //Lookup table for link records. (Entity) => LinkRecord
    public Dictionary<EntityBase, EntityRecord> LinkRecordsLookup = new Dictionary<EntityBase, EntityRecord>();

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
        Entities = new List<EntityBase>();
        return;
      }

      var session = OwnerRecord.Session;
      var listInfo = OwnerMember.ChildListInfo;
      // var linkRecs = session.GetChildRecords(OwnerRecord, listInfo.ParentRefMember);
      var linkRecs = session.ExecuteSelect(listInfo.SelectDirectChildList, OwnerRecord.PrimaryKey.Values);
      // Preload target entity records, in addition to link records.
      // These records will be cached in session, so when we try to read the target entity below it will be already loaded.
      // Preload is worth doing only if we have 2 or more link records.
      // Also, we do it only if record is NOT coming from full set cache; if it is, then target records should be in cache too,
      // so it does not make sense to cache them again in the current session.
      if (linkRecs.Count > 1 && OwnerRecord.EntityInfo.CacheType != CacheType.FullSet)
        session.ExecuteSelect(listInfo.SelectDirectChildList, OwnerRecord.PrimaryKey.Values);
      // Build LinkRecordsLookup and result entity list
      var entList = new List<EntityBase>();
      for (int i = 0; i < linkRecs.Count; i++) {
        var linkRec = linkRecs[i];
        var targetEnt = (EntityBase)linkRec.GetValue(listInfo.OtherEntityRefMember);
        entList.Add(targetEnt);
        LinkRecordsLookup[targetEnt] = linkRec;
      }
      Entities = entList;
    }

    //We do not create/modify link records when app code manipulates the list. Instead, we wait until Session.SaveChanges 
    // and then adjust link records for the entire list
    private void UpdateLinkRecords() {
      var list = Entities;
      var persistentOrderMember = OwnerMember.ChildListInfo.PersistentOrderMember;
      var unusedLinks = new HashSet<EntityRecord>(LinkRecordsLookup.Values); // initialize with all link records
      for (int i = 0; i < list.Count; i++) {
        var ent = (EntityBase)list[i];
        EntityRecord linkRec;
        if (LinkRecordsLookup.TryGetValue(ent, out linkRec)) {
          unusedLinks.Remove(linkRec);
        } else {
          linkRec = CreateLinkRecord(ent);
          LinkRecordsLookup[ent] = linkRec;
        }
        //Handle persistent ordering
        if (IsOrdered)
          linkRec.SetValueDirect(persistentOrderMember, i + 1, setModified: true); // Persistent order is 1-based
      }
      // Now handle deleted links - whatever is left in unusedLinks
      var session = OwnerRecord.Session;
      foreach (var delLink in unusedLinks)
        session.DeleteRecord(delLink);
      //to be completed
    }

    private EntityRecord CreateLinkRecord(EntityBase entity) {
      var listInfo = OwnerMember.ChildListInfo; 
      var linkRec = OwnerRecord.Session.NewRecord(listInfo.LinkEntity);
      linkRec.SetValue(listInfo.ParentRefMember.Index, OwnerRecord.EntityInstance);
      linkRec.SetValue(listInfo.OtherEntityRefMember.Index, entity);
      return linkRec;
    }

    public override void Init(IList entities, IList linkEntities = null) {
      base.Entities = entities;
      //Fill up link records lookup
      LinkRecordsLookup.Clear();
      var linkToTargetMember = this.OwnerMember.ChildListInfo.OtherEntityRefMember;
      foreach(EntityBase linkEnt in linkEntities) {
        var linkRec = EntityHelper.GetRecord(linkEnt);
        var targetEnt = linkRec.GetValue(linkToTargetMember) as EntityBase;
        LinkRecordsLookup[targetEnt] = linkRec; 
      }
      base.Modified = false;
    }

  }//class
}
