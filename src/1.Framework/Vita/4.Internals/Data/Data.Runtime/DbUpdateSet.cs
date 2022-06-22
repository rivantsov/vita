using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Data.Runtime {

  #region Notes
  /*
   Records in an update batch must be properly ordered, so that referentical integrity is not violated, ex: parent rec is inserted before child 
   record referencing it. To achieve proper ordering, a multi-step process is used. 
   First, records are classified into the following 5 groups (DbUpdateOrder enum): 
    1. Isolated deletes
    2. Inserts
    3. Updates
    4. Deletes
    5. Isolated inserts
  (more about isolated inserts/deletes below). 
  Updates are applied group-by-group in the listed order, IsolatedDeletes, then Inserts, then Updates, etc. 
  Within each update group, records (updates) must be sorted according to the topological order of tables/entities. 
  Topological order is built from table relations as part of building entity model, so each Entity has TopologicalIndex property assigned. 
  The sort order is direct or reverse depending on group type:
    1. Isolated deletes - direct topological order
    2. Inserts - reverse order
    3. Updates - no order is necessary but we keep direct order
    4. Deletes - direct order
    5. Isolated inserts - reverse order

  There is one special case with topological ordering - non-trivial topological groups, when 2 or more entities have the same topological index.
  This happens when tables have circular referential links 
      (ex: Employee references Department, Department has Manager property - reference back to Employee).
  Other case is when table is self-referencing (Employee has property ReportsTo which references other Employee record) - this table is also 
  marked as a non-trivial topological group. 
  So for each major group we collect all records that are non-trivial topologically. If there are any, these records are 'sorted' based on actual 
  relations between records (not tables), and SortSubIndex is assigned to each record. This sub-index will be used in 'comparison' of record in 
  final ordering pass when topological indexes at entity level are the same (see RecordSortCompare method). 
  Finally, once we have records sorted in topological order, there's another grouping we need. SQL allows us to perform multiple inserts in one 
  statement (insert many syntax); we also can delete multiple records with one SQL like 'Delete from Tbl where Id IN (....)'. To be able to do this, 
  we need to group sorted records into additional subgroups by target table - table groups. The actual update routine will create single SQLs for  
  insert and delete table groups. 
  This clumping together of records for the same table is achieved by using Entity name as a factor in sort comparer method (see RecordSortCompare).

  The structure of update set is the following. 
  DbUpdateSet contains list of DbUpdateGroup objects; 
  Each DbUpdateGroup contains list of DbUpdateTableGroup objects; 
  Each DbUpdateTableGroup contains individual records for a table. 

   About Isolated deletes and inserts ----------------------------------------------
   Isolated deletes and inserts are for entities that are not referenced by any other records; deletes are performed first, 
   and inserts are done last. This is a way to mitigate the conflict when some entity is deleted and "re-inserted" 
   - inserted new entity with the same primary key or with the same value of some unique key.
   The typical case for this is link records (in many-to-many relationships) - when user removes/adds link, or simply 
   the order is changed by inserting/deleting linked records. This delete/insert would not work if we do just inserts/updates/deletes 
   in proper order, like in general case
  */
  #endregion

  public enum DbUpdateOrder {
    IsolatedDelete = 0,
    Insert,
    Update,
    Delete,
    IsolatedInsert
  }

  [DebuggerDisplay("{GroupType}: {Records.Count} recs")]
  public class DbUpdateGroup {
    public DbUpdateOrder Order;
    public List<EntityRecord> Records = new List<EntityRecord>(); 
    public List<EntityRecord> RecordsForExtraSequencing; //created on demand
    public List<DbUpdateTableGroup> TableGroups = new List<DbUpdateTableGroup>();

    public DbUpdateGroup(DbUpdateOrder order) {
      Order = order; 
    }
  }


  [DebuggerDisplay("{Table.TableName}:{Records.Count} recs")]
  public class DbUpdateTableGroup {
    public DbTableInfo Table;
    public LinqOperation Operation; 
    public IList<EntityRecord> Records = new List<EntityRecord>();

    public DbUpdateTableGroup(DbTableInfo table, LinqOperation operation) {
      Table = table;
      Operation = operation;
    }
  }

  public class DbUpdateSet {
    public EntitySession Session; 
    public IList<EntityRecord> Records; //keeps all records that are affected by this batch
    public DataConnection Connection;
    public bool UseTransaction;
    public bool InsertsIdentity;
    public List<DbUpdateGroup> UpdateGroups = new List<DbUpdateGroup>();

    public DbUpdateSet(EntitySession session, DbModel dbModel, DataConnection conn) {
      Session = session;
      Connection = conn; 
      Records = session.RecordsChanged.Where(rec => ShouldUpdate(rec)).ToList();
      BuildRecordGroups(dbModel);
      var totalCount = Records.Count + Session.ScheduledCommandsCount();
      UseTransaction = totalCount > 1; 
    }

    #region Record sequencing and grouping
    private void BuildRecordGroups(DbModel dbModel) {
      //1. Classify records into major update groups
      foreach (var rec in this.Records)
        ClassifyRecord(rec);
      //2. sort records within each update group, create table groups
      foreach (var group in this.UpdateGroups) 
        if (group != null) {
          SequenceRecords(group);
          CreateTableGroups(dbModel, group);
        }
      // Sort groups
      UpdateGroups = UpdateGroups.OrderBy(g => g.Order).ToList(); 
    }

    private void ClassifyRecord(EntityRecord rec) {
      if(rec.Status == EntityStatus.New && rec.EntityInfo.Flags.IsSet(EntityFlags.HasIdentity))
        this.InsertsIdentity = true; 
      rec.SortSubIndex = 0;
      var order = GetGroupOrderForRecord(rec);
      var grp = GetCreateUpdateGroup(order);
      grp.Records.Add(rec);
      var needsExtraSeq = rec.EntityInfo.Flags.IsSet(EntityFlags.TopologicalGroupNonTrivial | EntityFlags.SelfReferencing);
      if (needsExtraSeq) {
        grp.RecordsForExtraSequencing = grp.RecordsForExtraSequencing ?? new List<EntityRecord>();
        grp.RecordsForExtraSequencing.Add(rec);
      }
    }

    private static void SequenceRecords(DbUpdateGroup group) {
      if (group == null || group.Records.Count < 2)
        return;
      // check if we need extra record sequencing, for records in non-trivial topological groups 
      var extraSeqRecords = group.RecordsForExtraSequencing;
      if (extraSeqRecords != null && extraSeqRecords.Count > 1)
        AssignSubIndexes(extraSeqRecords);
      // actually sort records by entity.TopologicalIndex, record.SubIndex, record.EntityName
      switch (group.Order) {
        case DbUpdateOrder.Update:
        case DbUpdateOrder.Delete:
        case DbUpdateOrder.IsolatedDelete:
          // direct order
          group.Records.Sort((x, y) => RecordsSortComparer(x, y, 1));
          break;
        case DbUpdateOrder.Insert:
        case DbUpdateOrder.IsolatedInsert:
          // reverse topological order
          group.Records.Sort((x, y) => RecordsSortComparer(x, y, -1));
          break;
      }
    }

    private static int RecordsSortComparer(EntityRecord x, EntityRecord y, int reverseFlag) {
      var cmp = x.EntityInfo.TopologicalIndex.CompareTo(y.EntityInfo.TopologicalIndex);
      if (cmp == 0)
        cmp = x.SortSubIndex.CompareTo(y.SortSubIndex);
      // if equal, we additionally compare by entity/table name, to group records for the same entity together in the sorted list,
      //  so that they will be merged into a table group
      if (cmp == 0)
        cmp = x.EntityInfo.Name.CompareTo(y.EntityInfo.Name);
      return cmp * reverseFlag;
    }

    private void CreateTableGroups(DbModel dbModel, DbUpdateGroup group) {
      DbUpdateTableGroup currGroup = null;
      foreach (var rec in group.Records) {
        if (currGroup == null || rec.EntityInfo != currGroup.Table.Entity) {
          var tbl = dbModel.GetTable(rec.EntityInfo.EntityType);
          currGroup = new DbUpdateTableGroup(tbl, ToOperation(group.Order));
          group.TableGroups.Add(currGroup);
        }
        currGroup.Records.Add(rec);
      } //foreach rec
    }

    private DbUpdateGroup GetCreateUpdateGroup(DbUpdateOrder order) {
      var group = UpdateGroups.FirstOrDefault(g => g.Order == order);
      if(group != null)
        return group;
      group = new DbUpdateGroup(order);
      UpdateGroups.Add(group);
      return group; 
    }

    //Sequence set of records in a looped non-trivial entity group. Assigns record.SortSubIndex value as a result of sequencing
    private static void AssignSubIndexes(IList<EntityRecord> records) {
      var graph = new SccGraph();
      foreach (var rec in records) {
        var ent = rec.EntityInfo;
        foreach (var refMember in ent.RefMembers) {
          //If rec is modified (not inserted/deleted) but reference is not changed then skip it
          if (rec.Status == EntityStatus.Modified && !rec.IsValueChanged(refMember))
            continue; 
          var targetEnt = rec.GetValue(refMember);
          //If target is null, nothing to do
          if ( targetEnt == null)
            continue;
          var targetRec = EntityHelper.GetRecord(targetEnt);
          // we are interested only in case when both records are inserted, or both are deleted.
          if (targetRec.Status != rec.Status)
            continue;
          // Both records must be in the same SCC group - i.e. have the same topological index
          if (targetRec.EntityInfo.TopologicalIndex != rec.EntityInfo.TopologicalIndex)
            continue;
          // We have potential conflict; add vertexes and link for the conflict
          var thisV = graph.FindOrAddVertex(rec);
          var targetV = graph.FindOrAddVertex(targetRec);
          thisV.AddLink(targetV);
        }
      }//foreach cmd
      //Check if any conflicts found
      if (graph.Vertexes.Count == 0)
        return;
      //Build SCC graph
      graph.BuildScc();
      // Once SCC is built, we have SCC indexes in Vertexes; use them to assign Record's SortSubIndex
      bool hasNonTrivialGroups = false;
      foreach (var v in graph.Vertexes) {
        var rec = (EntityRecord)v.Source;
        rec.SortSubIndex = rec.Status == EntityStatus.New ? v.SccIndex : -v.SccIndex; // -v.SccIndex : v.SccIndex;
        hasNonTrivialGroups |= v.NonTrivialGroup;
      }
      //if there are non-trivial groups, it means we have circular references in the set.
      if (hasNonTrivialGroups) {
        var entList = string.Join(",", records.Select(r => r.PrimaryKey.ToString()));
        var msg = Util.SafeFormat("Detected circular references between entities in an update set. Cannot commit group update. Entities: [{0}].", entList);
        var fault = new ClientFault() { Code = ClientFaultCodes.CircularEntityReference, Message = msg };
        var faultEx = new ClientFaultException(new[] { fault });
        faultEx.LogAsError = true;
        throw faultEx;
      }
    }

    private static DbUpdateOrder GetGroupOrderForRecord(EntityRecord record) {
      bool isIsolated = !record.EntityInfo.Flags.IsSet(EntityFlags.IsReferenced);
      switch (record.Status) {
        case EntityStatus.New:
          return isIsolated ? DbUpdateOrder.IsolatedInsert : DbUpdateOrder.Insert;
        case EntityStatus.Modified:
          return DbUpdateOrder.Update;
        case EntityStatus.Deleting:
          return isIsolated ? DbUpdateOrder.IsolatedDelete : DbUpdateOrder.Delete;
        default:
          Util.Throw("Fatal: Invalid record status, should not be in update set. Record: {0}", record);
          return default; //never happens
      }//switch
    }

    private static LinqOperation ToOperation(DbUpdateOrder groupType) {
      switch(groupType) {
        case DbUpdateOrder.Insert: case DbUpdateOrder.IsolatedInsert:
          return LinqOperation.Insert;
        case DbUpdateOrder.Delete: case DbUpdateOrder.IsolatedDelete:
          return LinqOperation.Delete;
        case DbUpdateOrder.Update:
        default: 
          return LinqOperation.Update;
      }
    }

    #endregion

    public static bool ShouldUpdate(EntityRecord record) {
      if(record.Status == EntityStatus.Modified && record.EntityInfo.Flags.IsSet(EntityFlags.NoUpdate))
        return false; //if for whatever reason we have such a record, just ignore it
      if(record.Status == EntityStatus.Fantom)
        return false;
      return true;
    }


  }//class

}
