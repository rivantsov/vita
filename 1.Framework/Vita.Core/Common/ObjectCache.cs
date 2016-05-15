using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Caching;


namespace Vita.Common {

  public class ObjectCache<TValue> where TValue: class {
    MemoryCache _cache;
    int _expirationSecs;

    public ObjectCache(string name, int expirationSecs = 30) {
      _expirationSecs = expirationSecs;
      _cache = new MemoryCache(name);
    }


    public void Add(string key, TValue value) {
      var item = new CacheItem(key, value);
      var policy = new CacheItemPolicy() { AbsoluteExpiration = DateTime.UtcNow.AddSeconds(_expirationSecs) };
      _cache.Add(item, policy);
    }

    public TValue Lookup(string key) {
      var value = _cache.Get(key);
      return (TValue)value; 
    }

    public void Remove(string key) {
      _cache.Remove(key); 
    }

    public void Clear() {
      _cache.Trim(100);
    }


  }//class
}//ns
