using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vita.Entities.Services;
using Vita.Entities.Utilities;
using Vita.Internals.Utilities;

namespace Vita.Entities.Logging {

  // For use in Web apps

  /// <summary>BufferedOperationLog accumulates entries internally.  </summary>
  public class BufferedLog : IBufferedLog {
    ILogService _logService;
    LogContext _logContext; 
    BufferingQueue<LogEntry> _queue;
    int _maxEntries;
    private int _errorCount;

    public BufferedLog(LogContext context = null, int maxEntries = 1000, ILogService logService = null) {
      _logContext = context ?? LogContext.SystemLogContext;
      _maxEntries = maxEntries;
      _queue = new BufferingQueue<LogEntry>();
      _logService = logService; 
    }

    public void AddEntry(LogEntry entry) {
      _queue.Enqueue(entry);
      if (entry.EntryType == LogEntryType.Error)
        Interlocked.Increment(ref _errorCount);
    }

    public void Flush() {
      if(_logService == null)
        return;
      var entries = _queue.DequeueMany(); 
      var compEntry = new BatchedLogEntry(_logContext, entries);
      _logService.AddEntry(compEntry);
    }

    public int ErrorCount => _errorCount;

    public IList<LogEntry> GetAll() => _queue.DequeueMany(); 

  } //class

}
