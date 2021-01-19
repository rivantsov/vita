using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Linq;

namespace Vita.Entities.Runtime {
  partial class EntitySession {
    public bool SmartLoadEnabled => Options.IsSet(EntitySessionOptions.EnableSmartLoad);

    internal bool TryReloadSiblingPackForRecordStub(EntityRecord record) {
      var parent = (EntityRecord)record.StubParentRef?.Target;
      var srcQuery = parent?.SourceQuery;
      if (srcQuery == null)
        return false;
      var fkMember = record.StubParentMember;


      var fkValues = GetMemberValues(records, fkMember);
      var selectCmd = LinqCommandFactory.CreateSelectByKeyValueArray(this, record.EntityInfo.PrimaryKey, null, fkValues);
      var entList = this.ExecuteLinqCommand(selectCmd, withIncludes: false);

      return true; 
    }


  } //class
}
