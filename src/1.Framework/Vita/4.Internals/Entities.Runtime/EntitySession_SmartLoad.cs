using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vita.Data.Linq;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {
  partial class EntitySession {
    public bool SmartLoadEnabled => Options.IsSet(EntitySessionOptions.EnableSmartLoad);

    internal QueryResultsWeakSet CurrentQueryResultsWeakSet;

  } //class
}
