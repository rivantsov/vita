using System;
using System.Collections.Generic;
using System.Linq;
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
        return false; // we can't do it with composite keys
      var fkMember = fkKey.ExpandedKeyMembers[0].Member;
      var records = srcQuery.RecordRefs.Select(r => (EntityRecord) r.Target)
           .Where(rec => rec != null && rec.EntityInfo == parEnt)
           .ToList();

      var fkValues = IncludeProcessor.GetDistinctMemberValues(records, fkMember);
      if (fkValues.Count == 0)
        return false; 
      var selectCmd = LinqCommandFactory.CreateSelectByKeyValueArray(this, record.EntityInfo.PrimaryKey, null, fkValues);
      // Once we execute the query, the target stubs will be reloaded; we do not actually need the resulting ent list
      var entList = this.ExecuteLinqCommand(selectCmd, withIncludes: false);
      return true; 
    }

  } //class
}
