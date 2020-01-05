using System;
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
    
    /// <summary>Entry types that should be passed to global log service. 
    /// By default includes Error and AppEvent types.  </summary>
    public static Type[] CriticalEntryTypes = new Type[] {
      typeof(ErrorLogEntry), typeof(AppEventEntry)
    };

    public BufferedLog(LogContext context = null, int maxEntries = 1000, ILogService logService = null) {
      _logContext = context ?? LogContext.SystemLogContext;
      _maxEntries = maxEntries;
      _logService = logService; 
    }

    public void AddEntry(LogEntry entry) {
      _queue.Enqueue(entry);
      // Check if it is critical entry and should be passed to global log service
      if(_logService != null && CriticalEntryTypes != null && CriticalEntryTypes.Contains(entry.GetType()))
        _logService.AddEntry(entry);
      // count errors
      if(entry.IsError)
        Interlocked.Increment(ref _errorCount);
    }

    public int ErrorCount => _errorCount;

    public IList<LogEntry> GetAll()  =>_queue.DequeueMany();

  } //class

}
