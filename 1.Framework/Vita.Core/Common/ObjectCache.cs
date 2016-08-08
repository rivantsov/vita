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
    public Action<string, TValue> OnRemoved;

    MemoryCache _cache;
    int _expirationSecs;
    CacheItemPolicy _slidingExpirationPolicy; 

    public ObjectCache(string name, int expirationSecs = 60, bool sliding = true) {
      _expirationSecs = expirationSecs;
      _cache = new MemoryCache(name);
      if(sliding)
        _slidingExpirationPolicy = new CacheItemPolicy() { RemovedCallback = OnCacheEntryRemoved, SlidingExpiration = TimeSpan.FromSeconds(_expirationSecs) };
    }

    public void Add(string key, TValue value) {
      var item = new CacheItem(key, value);
      var policy = _slidingExpirationPolicy ?? new CacheItemPolicy() {RemovedCallback = OnCacheEntryRemoved, AbsoluteExpiration = DateTime.UtcNow.AddSeconds(_expirationSecs)  };
      _cache.Add(item, policy);
    }

    void OnCacheEntryRemoved(CacheEntryRemovedArguments args) {
      OnRemoved?.Invoke(args.CacheItem.Key, (TValue) args.CacheItem.Value);
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
