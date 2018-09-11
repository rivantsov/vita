using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Utilities {

  // Used by OperationContext to store related Disposable objects that must be disposed when context is disposed.
  // Mainly used by DbConnections that are kept open by sessions
  public class ThreadSafeWeakRefList<T> where T: class {
    IList<WeakReference> _list = new List<WeakReference>();
    object _lock = new object();
    int _opCount; 

    public void AddRef(T target) {
      lock(_lock) {
        _list.Add(new WeakReference(target));
        _opCount++;
      }
    }
    public int Count {
      get { return _list.Count; }
    }


    public void ForEach(Action<T> action, bool clear) {
      lock(_lock) {
        for(int i = _list.Count-1; i >= 0; i--) {
          var target = (T) _list[i].Target;
          if(target != null)
            action(target);
        }
        if(clear)
          _list.Clear();
        else 
          IncrementOpCount(); 
      }// lock
    }

    public void Clear() {
      lock(_lock)
        _list.Clear(); 
    }

    public void Purge() {
      lock(_lock) {
        for(int i = _list.Count - 1; i >= 0; i--) 
          if(!_list[i].IsAlive)
            _list.RemoveAt(i);
        _opCount = 0; 
      }// lock
    }
    //assumes lock is held
    private void IncrementOpCount() {
      _opCount++;
      if(_opCount % 100 == 0 && _opCount > 0)
        Task.Run(() => Purge());
    }


  }//class

}
