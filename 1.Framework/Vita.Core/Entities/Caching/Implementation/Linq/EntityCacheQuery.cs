using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Entities.Caching {

  public class EntityCacheQuery {
    public readonly string LogString;
    public Func<EntitySession, FullSetEntityCache, object[], object> CacheFunc;

    public EntityCacheQuery(Func<EntitySession, FullSetEntityCache, object[], object> cacheFunc, string logString) {
      CacheFunc = cacheFunc;
      LogString = logString;
    }
  }

}
