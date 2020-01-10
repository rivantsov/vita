using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  /// <summary>Simply redirects all log entries to listeners.</summary>
  public class LogService : Observable<LogEntry>, ILogService {

    public void AddEntry(LogEntry entry) {
      Broadcast(entry);
      if(entry.IsError)
        Flush(); 
    }

    public void Flush() {
      ForEachSubscription(s => s.OnCompleted()); 
    }

  } //class 
}
