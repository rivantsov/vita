using System.Collections.Generic;
using System.Threading;
using Vita.Internals.Utilities;

namespace Vita.Entities.Logging {

  /// <summary>BufferedOperationLog accumulates entries internally.  </summary>
  public class BufferedLog : IBufferedLog {
    ILogService _logService;
    LogContext _logContext; 
    LinkedQueue _queue;
    int _maxEntries;
    private int _errorCount;

    public BufferedLog(LogContext context = null, int maxEntries = 1000, ILogService logService = null) {
      _logContext = context ?? LogContext.SystemLogContext;
      _maxEntries = maxEntries;
      _queue = new LinkedQueue();
      _logService = logService; 
    }

    public void AddEntry(LogEntry entry) {
      _queue.EnqueueNode(entry);
      if (entry.EntryType == LogEntryType.Error)
        Interlocked.Increment(ref _errorCount);
    }

    public void Flush() {
      if(_logService == null)
        return;
      var entries = GetAll(); 
      var compEntry = new BatchedLogEntry(_logContext, entries);
      _logService.AddEntry(compEntry);
    }

    public int ErrorCount => _errorCount;

    public IList<LogEntry> GetAll() => _queue.DequeueNodes<LogEntry>(); 

  } //class

}
