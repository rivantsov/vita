using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {
  partial class EntitySession {
    public bool SmartLoadEnabled => Options.IsSet(EntitySessionOptions.EnableSmartLoad);

    internal QueryResultsWeakSet CurrentQueryResultsWeakSet;

    internal bool TryReloadSiblingPackForRecordStub(EntityRecord record) {
      var parent = (EntityRecord)record.StubParentRef?.Target;
      var refMember = record.StubParentMember;
      if (parent == null || refMember == null)
        return false;
      var fkKey = refMember.ReferenceInfo.FromKey;
      if (fkKey.ExpandedKeyMembers.Count > 1)
        return false; // we can't do it with composite keys
      var fkMember = fkKey.ExpandedKeyMembers[0].Member;
      var records = GetLoadedSiblings(parent);
      if (records.Count == 0)
        return false;
      var fkValues = IncludeProcessor.GetDistinctMemberValues(records, fkMember);
      if (fkValues.Count == 0)
        return false;
      var selectCmd = LinqCommandFactory.CreateSelectByKeyValueArray(this, record.EntityInfo.PrimaryKey, null, fkValues);
      // Once we execute the query, the target stubs will be reloaded; we do not actually need the resulting ent list
      var entList = this.ExecuteLinqCommand(selectCmd, withIncludes: false);
      return true;
    }

    // Returns records loaded in the same query as the given record
    internal IList<EntityRecord> GetLoadedSiblings(EntityRecord rec) {
      var srcResultSet = rec?.SourceQueryResultSet;
      if (srcResultSet == null)
        return EntityRecord.EmptyList;
      var entInfo = rec.EntityInfo;
      var records = srcResultSet.RecordRefs.Select(r => (EntityRecord)r.Target)
         .Where(rec => rec != null && rec.EntityInfo == entInfo)
         .ToList();
      return records;
    }

    internal void LoadListManyToMany<T>(PropertyBoundListManyToMany<T> list) where T : class {
      var ownerRec = list.OwnerRecord;
      if (this.SmartLoadEnabled && ownerRec.SourceQueryResultSet != null) {
        LoadListManyToManyForSourceQuerySiblings(ownerRec, list.OwnerMember);
        if (list.IsLoaded)
          return;
      }
      list.Modified = false;
      list.LinkRecordsLookup.Clear();
      if (ownerRec.Status == EntityStatus.Fantom || ownerRec.Status == EntityStatus.New) {
        list.SetAsEmpty();
        return;
      }
      var listInfo = list.OwnerMember.ChildListInfo;
      var cmd = LinqCommandFactory.CreateSelectByKeyForListPropertyManyToMany(this, listInfo, ownerRec.PrimaryKey.Values);
      var queryRes = this.ExecuteLinqCommand(cmd);
      var tupleList = (IList<LinkTuple>)queryRes;
      list.SetItems(tupleList);
    }

    internal void LoadListManyToOne<T>(PropertyBoundListManyToOne<T> list) where T : class {
      list.Modified = false;
      var ownerRec = list.OwnerRecord;
      if (ownerRec.Status == EntityStatus.Fantom || ownerRec.Status == EntityStatus.New) {
        list.SetAsEmpty();
        return;
      }
      if (this.SmartLoadEnabled && ownerRec.SourceQueryResultSet != null) {
        LoadListManyToOneForSourceQuerySiblings(ownerRec, list.OwnerMember);
        if (list.IsLoaded)
          return;
      }
      var listInfo = list.OwnerMember.ChildListInfo;
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      var orderBy = listInfo.OrderBy;
      var selectCmd = LinqCommandFactory.CreateSelectByKeyForListPropertyManyToOne(this, listInfo,
                                             ownerRec.PrimaryKey.Values);
      var objEntList = (IList)this.ExecuteLinqCommand(selectCmd);
      var recContList = new List<IEntityRecordContainer>();
      foreach (var ent in objEntList)
        recContList.Add((IEntityRecordContainer)ent);
      list.SetItems(recContList);
    }

    internal void LoadListManyToOneForSourceQuerySiblings(EntityRecord record, EntityMemberInfo listMember) {
      var allRecs = GetLoadedSiblings(record);
      if (allRecs.Count == 0)
        return; 
      IncludeProcessor.RunIncludeForListManyToOne(record.Session, allRecs, listMember);
    }

    internal void LoadListManyToManyForSourceQuerySiblings(EntityRecord record, EntityMemberInfo listMember) {
      var allRecs = GetLoadedSiblings(record);
      if (allRecs.Count == 0)
        return;
      IncludeProcessor.RunIncludeForListManyToMany(record.Session, allRecs, listMember);

    }


  } //class
}
