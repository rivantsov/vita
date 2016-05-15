using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {
  /// <summary>A wrapper for dictionary tracking modified state of the dictionary.</summary>
  /// <typeparam name="TKey">Key type.</typeparam>
  /// <typeparam name="TValue">Value type.</typeparam>
  public class DictionaryWrapper<TKey,TValue> : IDictionary<TKey, TValue> {
    public bool Modified;
    IDictionary<TKey, TValue> _dict;

    public DictionaryWrapper(IDictionary<TKey, TValue> dict = null) {
      _dict = dict ?? new Dictionary<TKey, TValue>(); 
    }

    #region IDictionary members

    public void Add(TKey key, TValue value) {
      Modified = true;
      _dict.Add(key, value);
    }

    public bool ContainsKey(TKey key) {
      return _dict.ContainsKey(key); 
    }

    public ICollection<TKey> Keys {
      get { return _dict.Keys; }
    }

    public bool Remove(TKey key) {
      Modified = true; 
      return _dict.Remove(key);
    }

    public bool TryGetValue(TKey key, out TValue value) {
      return _dict.TryGetValue(key, out value); 
    }

    public ICollection<TValue> Values {
      get { return _dict.Values; }
    }

    public TValue this[TKey key] {
      get {
        return _dict[key];
      }
      set {
        _dict[key] = value;
        Modified = true; 
      }
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      Modified = true; 
      _dict.Add(item); 
    }

    public void Clear() {
      Modified = true; 
      _dict.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return _dict.Contains(item); 
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      _dict.CopyTo(array, arrayIndex);
    }

    public int Count {
      get { return _dict.Count; }
    }

    public bool IsReadOnly {
      get { return _dict.IsReadOnly; }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      Modified = true;
      return _dict.Remove(item); 
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return _dict.GetEnumerator(); 
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      var iDict = _dict as System.Collections.IEnumerable;
      return iDict.GetEnumerator();
    }

    #endregion
  }

}
