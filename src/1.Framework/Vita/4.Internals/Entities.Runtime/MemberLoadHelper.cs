﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data;
using Vita.Data.Linq;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Entities.Runtime {

  internal static class MemberLoadHelper {

    internal static IList<EntityRecord> GetRecordList(IList entityList) {
      var recList = new List<EntityRecord>();
      foreach (var ent in entityList) {
        var rec = ((IEntityRecordContainer)ent).Record;
        recList.Add(rec);
      }
      return recList;
    }

    // Returns records loaded in the same query as the given record
    internal static IList<EntityRecord> GetLoadedSiblings(EntityRecord rec) {
      var srcResultSet = rec?.SourceQueryResultSet;
      if (srcResultSet == null)
        return EntityRecord.EmptyList;
      var entInfo = rec.EntityInfo;
      var records = srcResultSet.RecordRefs.Select(r => (EntityRecord)r.Target)
         .Where(rec => rec != null && rec.EntityInfo == entInfo)
         .ToList();
      return records;
    }

    internal static bool TryReloadSiblingPackForRecordStub(EntityRecord record) {
      var parent = (EntityRecord)record.StubParentRef?.Target;
      var refMember = record.StubParentMember;
      if (parent == null || refMember == null)
        return false;
      var fkKey = refMember.ReferenceInfo.FromKey;
      if (fkKey.ExpandedKeyMembers.Count > 1)
        return false; // we can't do it with composite keys
      var records = GetLoadedSiblings(parent);
      if (records.Count == 0)
        return false;
      LoadEntityRefMember(record.Session, records, refMember);
      return true;
    }

    internal static void LoadListManyToMany<T>(PropertyBoundListManyToMany<T> list) where T : class {
      var ownerRec = list.OwnerRecord;
      var session = ownerRec.Session;
      if (session.SmartLoadEnabled && ownerRec.SourceQueryResultSet != null) {
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
      var cmd = LinqCommandFactory.CreateSelectByKeyForListPropertyManyToMany(session, listInfo, ownerRec.PrimaryKey.Values);
      var queryRes = session.ExecuteLinqCommand(cmd);
      var tupleList = (IList<LinkTuple>)queryRes;
      list.SetItems(tupleList);
    }

    internal static void LoadListManyToOne<T>(PropertyBoundListManyToOne<T> list) where T : class {
      var session = list.OwnerRecord.Session;
      list.Modified = false;
      var ownerRec = list.OwnerRecord;
      if (ownerRec.Status == EntityStatus.Fantom || ownerRec.Status == EntityStatus.New) {
        list.SetAsEmpty();
        return;
      }
      if (session.SmartLoadEnabled && ownerRec.SourceQueryResultSet != null) {
        LoadListManyToOneForSourceQuerySiblings(ownerRec, list.OwnerMember);
        if (list.IsLoaded)
          return;
      }
      var listInfo = list.OwnerMember.ChildListInfo;
      var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
      var orderBy = listInfo.OrderBy;
      var selectCmd = LinqCommandFactory.CreateSelectByKeyForListPropertyManyToOne(session, listInfo,
                                             ownerRec.PrimaryKey.Values);
      var objEntList = (IList)session.ExecuteLinqCommand(selectCmd);
      var recContList = new List<IEntityRecordContainer>();
      foreach (var ent in objEntList)
        recContList.Add((IEntityRecordContainer)ent);
      list.SetItems(recContList);
    }

    internal static void LoadListManyToOneForSourceQuerySiblings(EntityRecord record, EntityMemberInfo listMember) {
      var allRecs = GetLoadedSiblings(record);
      if (allRecs.Count == 0)
        return;
      LoadListManyToOneMember(record.Session, allRecs, listMember);
    }

    internal static void LoadListManyToManyForSourceQuerySiblings(EntityRecord record, EntityMemberInfo listMember) {
      var allRecs = GetLoadedSiblings(record);
      if (allRecs.Count == 0)
        return;
      LoadListManyToManyMember(record.Session, allRecs, listMember);
    }


    internal static IList<EntityRecord> LoadEntityRefMember(EntitySession session, IList<EntityRecord> records, EntityMemberInfo refMember) {
      if (records.Count == 0)
        return EntityRecord.EmptyList;
      var targetEntity = refMember.ReferenceInfo.ToKey.Entity;
      var fkMember = refMember.ReferenceInfo.FromKey.ExpandedKeyMembers[0].Member; // r.Book_Id
      var fkValues = GetDistinctMemberValues(records, fkMember);
      var selectCmd = LinqCommandFactory.CreateSelectByKeyValueArray(session, targetEntity.PrimaryKey, null, fkValues);
      var entList = (IList)session.ExecuteLinqCommand(selectCmd, withIncludes: false);
      if (entList.Count == 0)
        return EntityRecord.EmptyList;
      var recList = GetRecordList(entList);
      // Set ref members in parent records
      var targetPk = refMember.ReferenceInfo.ToKey;
      foreach (var parentRec in records) {
        var fkValue = parentRec.GetValueDirect(fkMember);
        if (fkValue == DBNull.Value) {
          parentRec.SetValueDirect(refMember, DBNull.Value);
        } else {
          var pkKey = new EntityKey(targetPk, fkValue);
          //we lookup in session, instead of searching in results of Include query - all just loaded records 
          //  are registered in session and lookup is done by key (it is fact dict lookup)
          var targetRec = session.GetRecord(pkKey);
          parentRec.SetValueDirect(refMember, targetRec);
        }
      }
      return recList;
    }

    // Example: records: List<IBookOrder>, listMember: bookOrder.Lines; so we load lines for each book order
    internal static IList<EntityRecord> LoadListManyToOneMember(EntitySession session, IList<EntityRecord> records, EntityMemberInfo listMember) {
      var pkInfo = listMember.Entity.PrimaryKey;
      var expMembers = pkInfo.ExpandedKeyMembers;
      Util.Check(expMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember);
      var pkMember = expMembers[0].Member; // IBookOrder.Id
      var pkValuesArr = GetDistinctMemberValues(records, pkMember);
      var listInfo = listMember.ChildListInfo;
      var parentRefMember = listInfo.ParentRefMember; //IBookOrderLine.Order
      var fromKey = parentRefMember.ReferenceInfo.FromKey;
      Util.Check(fromKey.ExpandedKeyMembers.Count == 1, "Composite keys are not supported in Include expressions; member: {0}", parentRefMember);
      var selectCmd = LinqCommandFactory.CreateSelectByKeyArrayForListPropertyManyToOne(session, listInfo, pkValuesArr);
      var childEntities = (IList)session.ExecuteLinqCommand(selectCmd, withIncludes: false); //list of all IBookOrderLine for BookOrder objects in 'records' parameter
      var childRecs = GetRecordList(childEntities);
      //setup list properties in parent records
      var fk = fromKey.ExpandedKeyMembers[0].Member; //IBookOrderLine.Order_Id
      var groupedRecs = childRecs.GroupBy(rec => rec.GetValueDirect(fk)); //each group is list of order lines for a single book order; group key is BookOrder.Id
      foreach (var g in groupedRecs) {
        var pkValue = new EntityKey(pkInfo, g.Key); // Order_Id -> BookOrder.Id
        var parent = session.GetRecord(pkValue); // BookOrder
        var childList = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if (childList != null && childList.IsLoaded)
          continue;
        if (childList == null)
          childList = parent.InitChildEntityList(listMember);
        var grpChildEntities = g.Select(r => r.EntityInstance).ToList();
        childList.SetItems(grpChildEntities);
      }
      // If for some parent records child lists were empty, we need set the list property to empty list, 
      // If it remains null, it will be considered not loaded, and app will attempt to load it again on first touch
      foreach (var parent in records) {
        var list = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList;
        if (list == null)
          list = parent.InitChildEntityList(listMember);
        if (!list.IsLoaded)
          list.SetAsEmpty();
      }
      return childRecs;
    }

    // Example: records: List<IBook>, listMember: book.Author; so we load authors list for each book
    internal static IList<EntityRecord> LoadListManyToManyMember(EntitySession session, IList<EntityRecord> records, EntityMemberInfo listMember) {
      var pkInfo = listMember.Entity.PrimaryKey;
      var keyMembers = pkInfo.ExpandedKeyMembers;
      Util.Check(keyMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember);
      var listInfo = listMember.ChildListInfo;

      // PK values of records
      var pkValues = GetDistinctMemberValues(records, keyMembers[0].Member);
      //run include query; it will return LinkTuple list
      var cmd = LinqCommandFactory.CreateSelectByKeyArrayForListPropertyManyToMany(session, listInfo, pkValues);
      var tuples = (IList<LinkTuple>)session.ExecuteLinqCommand(cmd, withIncludes: false);

      // Group by parent record, and push groups/lists into individual records
      var fkMember = listInfo.ParentRefMember.ReferenceInfo.FromKey.ExpandedKeyMembers[0].Member;
      var tupleGroups = tuples.GroupBy(t => EntityHelper.GetRecord(t.LinkEntity).GetValueDirect(fkMember)).ToList();
      foreach (var g in tupleGroups) {
        var pkValue = new EntityKey(pkInfo, g.Key); // Order_Id -> BookOrder.Id
        var parent = session.GetRecord(pkValue); // BookOrder
        var childList = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if (childList != null && childList.IsLoaded)
          continue;
        if (childList == null)
          childList = parent.InitChildEntityList(listMember);
        var groupTuples = g.ToList();
        childList.SetItems(groupTuples);
      }
      // Init/clear all lists that were NOT loaded
      var emptyTuples = new List<LinkTuple>();
      foreach (var rec in records) {
        var childList = rec.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if (childList != null && childList.IsLoaded)
          continue;
        if (childList == null)
          childList = rec.InitChildEntityList(listMember);
        childList.SetItems(emptyTuples);
      }
      // collect all target records as function result
      var targetRecords = tuples.Select(t => EntityHelper.GetRecord(t.TargetEntity)).ToList();
      return targetRecords;
    }

    internal static IList GetDistinctMemberValues(IList<EntityRecord> records, EntityMemberInfo member) {
      // using Distinct with untyped values - might be questionable, but it appears it works, 
      // including with value types
      var values = records.Select(r => r.GetValueDirect(member))
                      .Where(v => v != null && v != DBNull.Value)
                      .Distinct()
                      .ToArray();
      return values;
    }

    internal static QueryResultShape GetResultShape(EntityModel model, Type outType) {
      if (typeof(EntityBase).IsAssignableFrom(outType))
        return QueryResultShape.Entity;
      if (outType.IsGenericType) {
        var genArg0 = outType.GetGenericArguments()[0];
        if (typeof(IEnumerable).IsAssignableFrom(outType) && model.IsEntity(genArg0))
          return QueryResultShape.EntityList;
      }
      return QueryResultShape.Object; // don't know and don't care      
    }



  }
}