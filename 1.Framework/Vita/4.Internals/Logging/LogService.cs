using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Logging;

namespace Vita.Entities.Logging {

  /// <summary>Simply redirects all log entries to listeners.</summary>
  public class LogService : ILogService {

    public event EventHandler<LogEntryEventArgs> EntryAdded;
    public event EventHandler FlushRequested; 

    public void AddEntry(LogEntry entry) {
      EntryAdded?.Invoke(this, new LogEntryEventArgs(entry));
    }

    public void Flush() {
      FlushRequested?.Invoke(this, EventArgs.Empty);
    }

  } //class 
}
