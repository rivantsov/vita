﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Model;


namespace Vita.Entities.Runtime {

  [System.Diagnostics.DebuggerDisplay("{LinkEntity}")]
  public class LinkTuple {
    public object LinkEntity { get; set; }
    public object TargetEntity { get; set; }

    public static IList<LinkTuple> EmptyList = new LinkTuple[] { }; 
  }


  internal class PropertyBoundListManyToMany<TEntity> : PropertyBoundListBase<TEntity>  where TEntity : class {
    //Lookup table for link records. (Entity) => LinkRecord
    public Dictionary<EntityRecord, EntityRecord> LinkRecordsLookup = new Dictionary<EntityRecord, EntityRecord>();

    public PropertyBoundListManyToMany(EntityRecord ownerRecord, EntityMemberInfo ownerMember) : base(ownerRecord, ownerMember) { 
    }

    public override void Notify(BoundListEventType eventType) {
      var session = OwnerRecord.Session; 
      switch (eventType) {
        case BoundListEventType.SavingChanges:
          if (!IsLoaded || !Modified) return;
          UpdateLinkRecordsBeforeSave();
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

    public override void SetItems(object data) {
      SetItemsFromLinkTuples((IList<LinkTuple>)data);
    }
    public override void SetAsEmpty() {
      SetItemsFromLinkTuples(LinkTuple.EmptyList);
    }

    public override void LoadList() {
      MemberLoadHelper.LoadListManyToMany<TEntity>(this);
    }

    private void SetItemsFromLinkTuples(IList<LinkTuple> linkTuples) {
      LinkRecordsLookup.Clear();
      var listInfo = OwnerMember.ChildListInfo; 
      var entities = new List<IEntityRecordContainer>();
      foreach(var tpl in linkTuples) {
        var linkRec = EntityHelper.GetRecord(tpl.LinkEntity);
        var targetRec = EntityHelper.GetRecord(tpl.TargetEntity);
        linkRec.SetValueDirect(listInfo.OtherEntityRefMember, tpl.TargetEntity);
        entities.Add((IEntityRecordContainer)tpl.TargetEntity);
        LinkRecordsLookup[targetRec] = linkRec;
      }
      Entities = entities;
      base.Modified = false; 
    }

    // We do not create/modify link records when app code manipulates the list. Instead, we wait until Session.SaveChanges 
    // and then adjust link records for the entire list
    private void UpdateLinkRecordsBeforeSave() {
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
        if (IsOrdered) {
          object incValue = (i.GetType() == persistentOrderMember.DataType) ? i + 1 : 
              incValue = Convert.ChangeType(i + 1, persistentOrderMember.DataType);
          linkRec.SetValueDirect(persistentOrderMember, incValue, setModified: true); // Persistent order is 1-based
        }
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

  }//class
}
