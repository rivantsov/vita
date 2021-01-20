using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq;

namespace Vita.Entities.Runtime {
  partial class EntitySession {
    public bool SmartLoadEnabled => Options.IsSet(EntitySessionOptions.EnableSmartLoad);

    internal QueryResultsWeakSet CurrentQueryResultsWeakSet;

    internal bool TryReloadSiblingPackForRecordStub(EntityRecord record) {
      var parent = (EntityRecord)record.StubParentRef?.Target;
      var srcQuery = parent?.SourceQueryResultSet;
      if (srcQuery == null)
        return false;
      var parEnt = parent.EntityInfo; 
      var refMember = record.StubParentMember;
      var fkKey = refMember.ReferenceInfo.FromKey;
      if (fkKey.ExpandedKeyMembers.Count > 1)
        return false;
      var fkMember = fkKey.ExpandedKeyMembers[0].Member;
      var fkValues = new List<object>();
      foreach (var recRef in srcQuery.RecordRefs) {
        var parRec = (EntityRecord)recRef.Target;
        // Note: there might be records of multiple types in the mix (when query was a join and produced tuple of entities)
        //  we are interested in only records of the same type as original record
        if (parRec == null || parRec.EntityInfo != parEnt)
          continue;
        var fkValue = parRec.GetValue(fkMember); // this hypothetically might cause another reload
        if (fkValue == null || fkValue == DBNull.Value)
          continue;
        fkValues.Add(fkValue);
      }
      if (fkValues.Count == 0)
        return false; 
      var selectCmd = LinqCommandFactory.CreateSelectByKeyValueArray(this, record.EntityInfo.PrimaryKey, null, fkValues);
      // Once we execute the query, the target stubs will be reloaded; we do not actually need the resulting ent list
      var entList = this.ExecuteLinqCommand(selectCmd, withIncludes: false);
      return true; 
    }

  } //class
}
