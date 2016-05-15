using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Caching {

  public class CacheSettings {
    /// <summary>Local sparse cache is cache associated with OperationContext. It serves cached records to multiple entity sessions 
    /// associated with the context. For Web applications, the lifespan of the cache is one web request (same as operation context). 
    /// </summary>
    public readonly HashSet<Type> LocalSparseCacheTypes = new HashSet<Type>();
    /// <summary>Global sparse cache types. Sparse cache holds single records and associated with a particular database. </summary>
    public readonly HashSet<Type> SparseCacheTypes = new HashSet<Type>();
    /// <summary>Global full-set cache types. Full set cache holds entire tables in cache and serves all requests for data, 
    /// including LINQ queries. </summary>
    public readonly HashSet<Type> FullSetCacheTypes = new HashSet<Type>();

    public int SparseCacheCapacity; //Not used for now
    public int FullSetCacheExpirationSec;
    public int SparseCacheExpirationSec;
    public bool CacheEnabled;

    public CacheSettings() {
      Setup(); 
    }

    public void Setup(bool enabled = true, int fullSetExpirationSec = 5 * 60, int sparseCacheExpirationSec = 30,
                int sparseCacheCapacity = 10000) {
      CacheEnabled = enabled;
      FullSetCacheExpirationSec = fullSetExpirationSec;
      SparseCacheExpirationSec = sparseCacheExpirationSec;
      SparseCacheCapacity = sparseCacheCapacity; 
    }
    
    public bool HasTypes() {
      return FullSetCacheTypes.Count > 0 || SparseCacheTypes.Count > 0 || LocalSparseCacheTypes.Count > 0;
    }

    public void AddCachedTypes(CacheType cacheType, params Type[] entities) {
      if(entities == null || entities.Length == 0)
        return; 
      switch(cacheType) {
        case CacheType.None: break; 
        case CacheType.LocalSparse: this.LocalSparseCacheTypes.UnionWith(entities); break; 
        case CacheType.Sparse: this.SparseCacheTypes.UnionWith(entities); break; 
        case CacheType.FullSet: this.FullSetCacheTypes.UnionWith(entities); break; 
      }
    }
  } //class

}
