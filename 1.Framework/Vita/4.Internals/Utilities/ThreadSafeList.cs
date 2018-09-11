using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Utilities {

  // Used by OperationContext to store related Disposable objects that must be disposed when context is disposed.
  // Mainly used by DbConnections that are kept open by sessions
  public class ThreadSafeList<T> where T: class {
    IList<T> _list = new List<T>();
    object _lock = new object();

    public void Add(T item) {
      lock(_lock) {
        _list.Add(item);
      }
    }

    public void ForEach(Action<T> action, bool clear) {
      lock(_lock) {
        for(int i = 0; i < _list.Count; i++) {
            action(_list[i]);
        }
        if(clear)
          _list.Clear();
      }// lock
    }

    public void Clear() {
      lock(_lock)
        _list.Clear(); 
    }

    public int Count {
      get {
        lock(_lock)
          return _list.Count;
      }
    }

    public T[] ToArray(bool clear = false) {
      lock(_lock) {
        var arr = _list.ToArray();
        if(clear)
          _list.Clear();
        return arr; 
      }
    }
  }//class

}
