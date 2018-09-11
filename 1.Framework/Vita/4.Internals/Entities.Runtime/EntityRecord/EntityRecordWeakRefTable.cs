using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Concurrent;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {

  // Note:   System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue> does not work here.
  // It does not compare TKey instances for equality,  it does not use TKey.GetHashCode() or Equals() 
  // - it requires exactly the same instance of TKey to retrieve the stored value. 

  public class EntityRecordWeakRefTable {
    // # of reads to trigger Cleanup
    private const int ReadCountTrigger = 1000;
    // min # of entries to run dictionary cleanup
    private const int VolumeThreshold = 200; 
    private int _readCount; //used for triggering cleanup
    private IDictionary<EntityKey, WeakReference> _table;

    public EntityRecordWeakRefTable(bool asConcurrent = false) {
      if (asConcurrent)
        _table = new ConcurrentDictionary<EntityKey, WeakReference>();
      else 
        _table = new Dictionary<EntityKey, WeakReference>();
    }

    public int Count {
      get {return _table.Count;}
    }

    public ICollection<EntityKey> Keys {
      get {return _table.Keys;}
    }

    //If record with the same PK value is already in dictionary, does not add but returns existing one.
    public EntityRecord Add(EntityRecord record) {
      var oldRec = Find(record.PrimaryKey);
      if (oldRec != null)
        return oldRec; 
      var weakRef = new WeakReference(record);
      _table[record.PrimaryKey] = weakRef;
      return record;
    }

    public EntityRecord Find(EntityKey primaryKey) {
      _readCount++; 
      if (_readCount > ReadCountTrigger && Count > VolumeThreshold)
        Cleanup(); 
      WeakReference recRef;
      if(_table.TryGetValue(primaryKey, out recRef)) {
        var target = recRef.Target;
        if(target != null) 
          return (EntityRecord) target;
        else 
          _table.Remove(primaryKey);
      }
      return null; 
    }

    public bool TryRemove(EntityKey key) {
      return _table.Remove(key);
    }

    //Remove entries holding nullified (GC-ed) references
    object _lock = new object(); 

    private void Cleanup() {
      lock (_lock) {
        var keysToRemove = new List<EntityKey>();
        foreach (var dictEntry in _table)
          if (dictEntry.Value.Target == null)
            keysToRemove.Add(dictEntry.Key);
        foreach (var key in keysToRemove)
          _table.Remove(key);
        _readCount = 0;
      }
    }

    public void Clear() {
      lock (_lock) {
        _table.Clear(); 
      }
    }

  }//class
}//namespace
