using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Vita.Entities.Logging {

  /// <summary>Accumulates entries, counts number of errors.  </summary>
  public class BufferedLog : IBufferedLog {
    ILogService _logService;
    LogContext _logContext; 
    int _maxEntries;
    int _errorCount;
    BatchingQueue<LogEntry> _queue = new BatchingQueue<LogEntry>();

    public BufferedLog(LogContext context = null, int maxEntries = 1000, ILogService logService = null) {
      _logContext = context ?? LogContext.SystemLogContext;
      _maxEntries = maxEntries;
      _logService = logService; 
    }

    public void AddEntry(LogEntry entry) {
      _queue.Enqueue(entry);
      if (entry.IsError)
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

    public IList<LogEntry> GetAll()  =>_queue.DequeueMany();

  } //class

}
