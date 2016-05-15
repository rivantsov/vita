using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {

  /// <summary>
  /// Used in OperationContext for tracking DataConnection object(s) hold by entity sessions, to ensure that 
  /// all connections are closed before context is disposed.
  /// </summary>
  public class DisposableWeakRefSet {

    List<WeakReference> _list = new List<WeakReference>();
    int _addCount;
    object _lock = new object();
    bool _hasData;

    public void Add(IDisposable value) {
      if (value == null)
        return;
      lock (_lock) {
        _list.Add(new WeakReference(value));
        _addCount++;
        if (_addCount > 5) 
          Cleanup();
        _hasData = _list.Count > 0; 
      }
    }

    // Method assumes we hold a lock
    private void Cleanup() {
      for (int i = _list.Count - 1; i >= 0; i--) {
        var obj = _list[i];
        if (obj.Target == null)
          _list.RemoveAt(i); 
      }
      _addCount = 0; 
    }

    public void DisposeAll() {
      if (!_hasData)
        return; 
      lock (_lock) {
        foreach (var wr in _list) {
          var disp = wr.Target as IDisposable;
          if (disp != null)
            disp.Dispose(); 
        } //foreach
        _list.Clear();
        _hasData = false; 
      }
    }//method

  }
}
