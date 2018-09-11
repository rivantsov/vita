using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;

namespace Vita.Entities.Logging {

  /// <summary>Simply redirects all log entries to listeners.</summary>
  public class DefaultLogService : ILogService {
    ILogListener[] _listeners = new ILogListener[] { }; 

    public void AddEntry(LogEntry entry) {
      var arr = _listeners;
      if(arr.Length == 1)
        arr[0].AddEntry(entry);
      else
        for(int i = 0; i < arr.Length; i++)
          arr[i].AddEntry(entry); 
    }

    public void AddListener(ILogListener listener) {
      var lst = _listeners.ToList();
      lst.Add(listener);
      _listeners = lst.ToArray(); 
    }

    public void RemoveListener(ILogListener listener) {
      var lst = _listeners.ToList();
      if (lst.Contains(listener))
        lst.Remove(listener);
      _listeners = lst.ToArray();
    }

  } //class 
}
