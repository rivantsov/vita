using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Vita.Entities.Utilities {

  public class DoubleBufferCache<TKey, TValue> {
    private ConcurrentDictionary<TKey, TValue> _frontSet = new ConcurrentDictionary<TKey, TValue>();
    private ConcurrentDictionary<TKey, TValue> _backSet = new ConcurrentDictionary<TKey, TValue>();

    private int _capacity;
    private long _lastSwapOn;
    private long _expirationTicks;
    private object _swapLock = new object(); 
    
    public DoubleBufferCache(int capacity = 1000, int expireSec = 10) {
      _capacity = capacity;
      _expirationTicks = expireSec * Stopwatch.Frequency; // frequency is ticks per sec
    }

    public TValue Lookup(TKey key, Func<TKey, TValue> cacheMissFunc = null) {
      if (_frontSet.TryGetValue(key, out var value))
        return value; 
      if (_backSet.TryGetValue(key, out value)) {
        Add(key, value); 
        return value; 
      }
      if (cacheMissFunc == null)
        return default(TValue);
      value = cacheMissFunc(key);
      if (EqualityComparer<TValue>.Default.Equals(value))
        return value;
      Add(key, value);
      return value; 
    }

    public void Add(TKey key, TValue value) {
      _frontSet.TryAdd(key, value);
      if (_frontSet.Count > _capacity || Stopwatch.GetTimestamp() > _lastSwapOn + _expirationTicks)
        SwapBuffers();
    }

    private bool _needToSwap;

    private void SwapBuffers() {
      _needToSwap = true; 
      lock(_swapLock) {
        if (!_needToSwap)
          return; //in case it was just swapped in another thread; to prevent from occasional multiple swaps
        // we consider assignments to be atomic operations
        var front = _frontSet;
        var back = _backSet;
        _backSet = front;
        back.Clear();
        _frontSet = back; 
        _lastSwapOn = Stopwatch.GetTimestamp();
        _needToSwap = false;
      }
    }

  }
}
