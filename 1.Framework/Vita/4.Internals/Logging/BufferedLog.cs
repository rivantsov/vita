using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  // For use in Web apps

  /// <summary>BufferedOperationLog accumulates entries internally.  </summary>
  public class BufferedLog : IBufferingLog {
    ILogService _logService;
    LogContext _logContext; 
    BatchingQueue<LogEntry> _queue;
    private int _errorCount;

    public BufferedLog(LogContext context = null, int maxEntries = 1000, ILogService logService = null) {
      _logContext = context ?? LogContext.SystemLogContext;
      _queue = new BatchingQueue<LogEntry>(maxEntries);
      _queue.Batched += Queue_Batched;
      _logService = logService; 
    }

    private void Queue_Batched(object sender, QueueBatchEventArgs<LogEntry> e) {
      var compEntry = new BatchedLogEntry(_logContext, e.Items);
      _logService.AddEntry(compEntry);
    }

    public void AddEntry(LogEntry entry) {
      _queue.Enqueue(entry);
      if (entry.EntryType == LogEntryType.Error)
        Interlocked.Increment(ref _errorCount);
    }

    public void Flush() {
      if(_logService == null)
        return;
      _queue.ProduceBatch();
    }

    public int ErrorCount => _errorCount;

    public IList<LogEntry> GetAll() => _queue.GetAll(); 

  } //class

}
