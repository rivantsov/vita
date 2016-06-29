using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Vita.Common;
using Vita.Common.Graphs;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Model;

namespace Vita.Data {

  public class BatchDbCommand {
    public IDbCommand DbCommand;
    List<Action> _postActions; //we try to optimize and not create action list if not necessary (most often)

    public void AddPostAction(Action action) {
      if (_postActions == null)
        _postActions = new List<Action>(); 
      _postActions.Add(action); 
    }
    public bool HasActions { 
      get { return _postActions != null && _postActions.Count > 0; } 
    }
    public List<Action> GetPostActions() {
      return _postActions; 
    }
  }

  public class UpdateSet { 
    public readonly Guid Id; //unique batch Id
    public readonly DataConnection Connection; 
    public readonly EntitySession Session;
    public bool UseTransaction;
    public readonly DateTime UpdateTime;
    public Dictionary<string, object> CustomData = new Dictionary<string, object>(); 

    public readonly HashSet<EntityInfo> EntityInfos = new HashSet<EntityInfo>();

    public readonly List<EntityRecord> AllRecords = new List<EntityRecord>(); //keeps all records that are affected by this batch
    public bool IsSequenced;
    //True if any of the update/insert methods use Out parameter. In this case batch mode may not be used for some providers (MySql, Postgres)
    public bool UsesOutParams;
    public List<BatchDbCommand> BatchCommands; //batch mode only

    public UpdateSet(DataConnection connection, DateTime updateTime, IList<EntityRecord> records, bool sequence) {
      Connection = connection;
      Session = connection.Session;
      UpdateTime = updateTime;
      Id = Guid.NewGuid();
      //Start explicit transaction only if we have more than one command to execute.
      UseTransaction = (records.Count + Session.ScheduledCommands.Count) > 1;
      AllRecords.AddRange(records);
      if (sequence)
        SequenceRecords(); //fills entity info set
      else {
        // fill entity Info set
        EntityInfos.UnionWith(records.Select(r => r.EntityInfo));
      }
      UsesOutParams = AllRecords.Any(r => UsesOutParam(r));
    }

    public object GetCustomData(string key) {
      object data;
      CustomData.TryGetValue(key, out data);
      return data; 
    }

    // Base indexes for record sort indexes in topological sorting
    const int Million = 1000000;
    const int BaseIsolatedDeletes = 1 * Million;
    const int BaseInserts = 2 * Million;
    const int BaseUpdates = 3 * Million;
    const int BaseDeletes = 4 * Million;
    const int BaseIsolatedInserts = 5 * Million;

    // The proper sequence of updates:
    //  1. Isolated deletes, in direct order
    //  2. Inserts in the reverse topological order
    //  3. Updates; no order is necessary but we keep direct order
    //  4. Deletes in direct topological order
    //  5. Isolated inserts in reverse order
    // Note about isolated deletes and inserts.
    // Isolated deletes and inserts are for entities that are not referenced by any other records; deletes are performed first, 
    // and inserts are done last. This is a way to mitigate the conflict when some entity is deleted and "re-inserted" 
    // - inserted new entity with the same primary key or with the same value of some unique key.
    // The typical case for this is link records (in many-to-many relationships) - when user removes/adds link, or simply 
    // the order is changed by inserting/deleting linked records. This delete/insert would not work if we do just inserts/updates/deletes 
    // in proper order, like in general case

    public void SequenceRecords() {
      // List of records that are in non-trivial topological groups; these records must be sorted between themselves, in addition to sort in table's topological order
      // ex: self-referencing records, like employee.Manager->employee
      var recordsToSort = new List<EntityRecord>();
      foreach(var rec in AllRecords) {
        var entInfo = rec.EntityInfo;
        EntityInfos.Add(entInfo); 
        rec.SortSubIndex = 0;
        var entIndex = rec.EntityInfo.TopologicalIndex;
        bool isIsolated = !entInfo.Flags.IsSet(EntityFlags.IsReferenced);
        var recordSequencing = entInfo.Flags.IsSet(EntityFlags.TopologicalGroupNonTrivial | EntityFlags.SelfReferencing);
        if (recordSequencing)
          recordsToSort.Add(rec); 
        switch(rec.Status) {
          case EntityStatus.New: 
            //Inserts are done in reverse topological order, so we are 
            rec.SortIndex = isIsolated ? BaseIsolatedInserts - entIndex : BaseInserts - entIndex; 
            break;
          case EntityStatus.Modified: 
            rec.SortIndex = BaseUpdates + entIndex; 
            break;
          case EntityStatus.Deleting: 
            rec.SortIndex = isIsolated ? BaseIsolatedDeletes + entIndex : BaseDeletes + entIndex; 
            break;
        }//switch
      }
      //Check if there are non-trivial subgroups - records for entities with the same topological index.
      // These are entities that are either self-referencing (ex: employee.Manager->employee) or sets of entities with ref loop (employee->department->Head(employee))
      // In this case sorting by table's topological index is not enough,
      // For these records we need to analyze record dependencies and order them properly
      if (recordsToSort.Count > 1)
        SequenceSubGroup(recordsToSort);
      //Finally sort them all using sort index and sort subIndex
      AllRecords.Sort(CompareTopologicalIndexes);
      IsSequenced = true; 
    }

    //Sequences set of records in a looped non-trivial entity group. Assigns record.SortSubIndex value after sequencing
    private void SequenceSubGroup(IEnumerable<EntityRecord> records) {
      var graph = new Graph();
      foreach (var rec in records) {
        var ent = rec.EntityInfo;
        foreach (var refMember in ent.RefMembers) {
          var targetEnt = rec.GetValue(refMember); 
          //If reference is not modified or not set, then nothing to do
          if (targetEnt == null)
            continue;
          var targetRec = EntityHelper.GetRecord(targetEnt);
          // we are interested only in case when both records are inserted, or both are deleted.
          if (targetRec.Status != rec.Status)
            continue;
          // finally, the target record's table must be in the same SCC group - i.e. have the same SccIndex
          if (targetRec.EntityInfo.TopologicalIndex != rec.EntityInfo.TopologicalIndex)
            continue;
          // We have potential conflict; add vertexes and link for the conflict
          var thisV = graph.FindOrAdd(rec);
          var targetV = graph.FindOrAdd(targetRec);
          thisV.AddLink(targetV);
        }
      }//foreach cmd
      //Check if any conflicts found
      if (graph.Vertexes.Count == 0) return;
      //Build SCC graph
      graph.BuildScc();
      // Once SCC is built, we have SCC indexes in Vertexes; use them to assign Record's TopologicalIndex
      bool hasNonTrivialGroups = false;
      foreach (var v in graph.Vertexes) {
        var rec = (EntityRecord)v.Tag;
        rec.SortSubIndex = rec.Status == EntityStatus.New ? -v.SccIndex : v.SccIndex;
        hasNonTrivialGroups |= v.NonTrivialGroup;
      }
      //if there are non-trivial groups, it means we have circular references in the set.
      if (hasNonTrivialGroups) {
        var entList = string.Join(",", records.Select(r=> r.PrimaryKey.ToString()));
        var msg = StringHelper.SafeFormat("Detected circular references between entities in an update set. Cannot commit group update. Entities: [{0}].", entList);
        var fault = new ClientFault() {Code = ClientFaultCodes.CircularEntityReference, Message = msg};
        var faultEx = new ClientFaultException(new[] { fault });
        faultEx.LogAsError = true; 
        throw faultEx; 
      }
    }

    public static int CompareTopologicalIndexes(EntityRecord x, EntityRecord y) {
      var entIndexCompare = x.SortIndex.CompareTo(y.SortIndex);
      if (entIndexCompare != 0) 
        return entIndexCompare;
      return x.SortSubIndex.CompareTo(y.SortSubIndex);
    }

    private static bool UsesOutParam(EntityRecord record) {
      var flags = record.EntityInfo.Flags;
      return flags.IsSet(EntityFlags.HasRowVersion) || (flags.IsSet(EntityFlags.HasIdentity) && record.Status == EntityStatus.New);
    }


  }// class


}
