using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vita.Entities.Runtime.SmartLoad;
using Vita.Entities.Utilities;

namespace Vita.Entities.Runtime {

  partial class EntitySession {

  }

  public class SourceQuery {
    public WeakReference[] RecordRefs;
  }

  partial class EntityRecord {
    public SourceQuery SourceQuery;
    public EntityReadTracker ReadTracker;
  }


}
