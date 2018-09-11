using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services;
using System.Threading;

namespace Vita.Entities.Utilities {

  public class ObjectCache<TKey, TValue> where TValue : class {
    #region nested CacheItem class
    class CacheItem {
      public readonly TKey Key;
      public readonly TValue Value;
      public readonly long AddedOnMs;
      public long LastUsedOnMs;

      public CacheItem(TKey key, TValue value, long timeMs) {
        Key = key;
        Value = value;
        AddedOnMs = LastUsedOnMs = timeMs; 
      }
    }

    public class CacheItemRemovedEventArgs : EventArgs {
      public readonly TKey Key;
      public readonly TValue Value;
      public CacheItemRemovedEventArgs(TKey key, TValue value) {
        Key = key;
        Value = value; 
      }
    }

    #endregion 

    public event EventHandler<CacheItemRemovedEventArgs> Removed; 

    ConcurrentDictionary<TKey, CacheItem> _items = new ConcurrentDictionary<TKey, CacheItem>();
    int _expiratonMs;
    int _maxLifeMs;
    long _lastPurgedOn;
    ITimeService _timeService;

    public ObjectCache(int expirationSeconds = 10, int maxLifeSeconds = 30) {
      _maxLifeMs = maxLifeSeconds * 1000;
      _expiratonMs = expirationSeconds * 1000;
      _timeService = Services.Implementations.TimeService.Instance;
    }

    public void Add(TKey key, TValue value) {
      var nowMs = _timeService.ElapsedMilliseconds;
      var item = new CacheItem( key, value, nowMs);
      _items[key] = item;
      CheckNeedPurge(nowMs);
    }

    public TValue Lookup(TKey key) {
      TValue value;
      if(TryGetValue(key, out value))
        return value;
      return null; 
    }

    public bool TryGetValue(TKey key, out TValue value) {
      var msNow = _timeService.ElapsedMilliseconds;
      CheckNeedPurge(msNow);
      CacheItem item;
      value = null;
      if(!_items.TryGetValue(key, out item))
        return false;
      if(CheckValid(item, msNow)) {
        value = item.Value;
        return true;
      }
      return false; 
    }

    public void Remove(TKey key) {
      CacheItem item;
      _items.TryRemove(key, out item);
      if(item != null && Removed != null)
        Removed(this, new CacheItemRemovedEventArgs(item.Key, item.Value));
    }

    public void Clear() {
      _items.Clear();
    }

    public int Count {
      get { return _items.Count; }
    }

    public override string ToString() {
      return "Count=" + Count;
    }

    private bool CheckValid(CacheItem item, long nowMs) {
      // Check lifetime since added
      var valid = item.AddedOnMs + _maxLifeMs > nowMs;
      if(valid) {
        var lastUsed = Interlocked.Exchange(ref item.LastUsedOnMs, nowMs);
        valid = (lastUsed + _expiratonMs > nowMs);
      }
      if(valid)
        return true;
      Remove(item.Key);
      return false;
    }


    #region Purging 
    bool _purging;
    public void CheckNeedPurge(long msNow) {
      if(_purging)
        return;
      var lastPurged = Interlocked.Read(ref _lastPurgedOn);
      if(lastPurged + _expiratonMs / 2 < msNow)
        Purge(msNow); 
    }

    public void Purge() {
      Purge(_timeService.ElapsedMilliseconds); 
    }

    private void Purge(long msNow) {
      if(_purging)
        return; 
      try {
        _purging = true; 
        var items = _items.Values;
        foreach(var item in items.ToList())
          CheckValid(item, msNow);
        Interlocked.Exchange(ref _lastPurgedOn, msNow);
      } finally {
        _purging = true; 
      }
    }
    #endregion

  }//class
}//ns
