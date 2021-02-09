using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Vita.Entities.Model;
using Vita.Entities.Runtime.SmartLoad;
using Vita.Entities.Utilities;

namespace Vita.Entities.Runtime {

  [DebuggerDisplay("Count:{RecordRefs.Count}")]
  public class QueryResultsWeakSet {
    public IList<WeakReference> RecordRefs = new List<WeakReference>();
  }

  partial class EntityRecord {
    // public QueryResultsWeakSet SourceQueryResultSet;
    // public WeakReference StubParentRef;
    public EntityMemberInfo StubParentMember; 
  }


}
