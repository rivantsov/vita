using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vita.Entities.Model;
using Vita.Entities.Runtime.SmartLoad;
using Vita.Entities.Utilities;

namespace Vita.Entities.Runtime {

  partial class EntitySession {

  }

  public class QueryResultsWeakSet {
    public IList<WeakReference> RecordRefs = new List<WeakReference>();
  }

  partial class EntityRecord {
    public QueryResultsWeakSet SourceQueryResultSet;
    public EntityReadTracker ReadTracker;

    public WeakReference StubParentRef;
    public EntityMemberInfo StubParentMember; 
  }


}
