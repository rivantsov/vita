using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vita.Entities.Runtime.SmartLoad;
using Vita.Entities.Utilities;

namespace Vita.Entities.Runtime {
  partial class EntitySession {
    int _currentRecordId;

    public int NextRecordId() {
      return Interlocked.Increment(ref _currentRecordId);
    }
  }

  partial class EntityRecord {
    public int IntRecordId;
    public SourceQuery SourceQuery;
    public BitMask MembersRead; 
  }

  partial class EntitySesion {

  }

}
