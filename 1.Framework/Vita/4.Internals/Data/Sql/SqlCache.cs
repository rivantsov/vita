using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Data.Sql;
using Vita.Entities.Services.Implementations;

namespace Vita.Data.Sql {

  public class SqlCache {

    class SqlCacheItem {
      public SqlStatement Sql;
      public long LastUsed; 
    }

    ConcurrentDictionary<SqlCacheKey, SqlCacheItem> _cache;
    TimeService _timeService;
    int _capacity;

    public static Action<SqlCacheKey, SqlStatement> Debug_OnLookup; 

    public SqlCache(int capacity = 10000) {
      _capacity = capacity; 
      _timeService = TimeService.Instance;
      _cache = new ConcurrentDictionary<SqlCacheKey, SqlCacheItem>();
      _lastSizeCheck = _timeService.ElapsedMilliseconds; 
    }

    public void Add(SqlCacheKey key, SqlStatement sql) {
      if (!sql.IsCompacted)
        sql.Compact();
      var now = _timeService.ElapsedMilliseconds;
      CheckSize(now);
      var item = new SqlCacheItem() { LastUsed = now, Sql = sql };
      _cache.TryAdd(key, item); 
    }

    public SqlStatement Lookup(SqlCacheKey key) {
      Interlocked.Increment(ref LookupCount);
      if(!_cache.TryGetValue(key, out var item)) {
        Interlocked.Increment(ref MissCount);
        Debug_OnLookup?.Invoke(key, null); 
        return null;
      }
      Interlocked.Exchange(ref item.LastUsed, _timeService.ElapsedMilliseconds);
      Debug_OnLookup?.Invoke(key, item.Sql);
      return item.Sql; 
    }

    private long _lastSizeCheck;
    private void CheckSize(long now) {
      if(now - _lastSizeCheck < 100) // do not do it too oftern, every 100m at most
        return;
      var count = _cache.Count;
      Interlocked.Exchange(ref _lastSizeCheck, now);
      if(count < _capacity * 4 / 5) //if over 80/% only
        return; 
      Task.Run(() => Trim(now));
    }

    private void Trim(long now) {
      var all = _cache.ToArray();
      var cutoff = now - 100;
      var toRemove = all.Where(de => de.Value.LastUsed < cutoff)
                        .OrderBy(de => de.Value.LastUsed)
                        .Take(_capacity / 5) //20% of entries
                        .ToList();
      foreach(var de in toRemove)
        _cache.TryRemove(de.Key, out var dummy);
    }

    //stats
    public static long LookupCount;
    public static long MissCount;

    public static void ResetStats() {
      LookupCount = 0;
      MissCount = 0; 
    }

  }//class

}
