using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;

namespace Vita.Entities.Logging {

  /// <summary>Simply redirects all log entries to listeners.</summary>
  public class LogService : ILogService {
    class ListenerInfo {
      public ILogListener Listener;
      public Func<LogEntry, bool> Filter;
    }

    ListenerInfo[] _listeners = new ListenerInfo[] {}; 

    public void AddEntry(LogEntry entry) {
      var arr = _listeners;
      for(int i = 0; i < arr.Length; i++) {
        var info = arr[i];
        if (info.Filter != null && !info.Filter(entry))
          continue;
        info.Listener.AddEntry(entry); 
      }
    }

    public void Flush() {
      var arr = _listeners;
      for (int i = 0; i < arr.Length; i++)
          arr[i].Listener.Flush();
    }

    public void AddListener(ILogListener listener, Func<LogEntry, bool> filter = null) {
      var lst = _listeners.ToList();
      lst.Add(new ListenerInfo() { Listener = listener, Filter = filter });
      _listeners = lst.ToArray(); 
    }

    public void RemoveListener(ILogListener listener) {
      var lst = _listeners.ToList();
      _listeners = lst.Where(li => li.Listener != listener).ToArray();
    }

  } //class 
}
