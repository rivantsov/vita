using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  public class LogFileWriter : IDisposable {
    string _fileName;
    ILogService _logService;
    Func<LogEntry, bool> _filter;
    IDisposable _logSubscription; 
    bool _disposed;

    ActiveBatchingBuffer<LogEntry> _buffer; 
    static object _fileWriteLock = new object(); 

    public LogFileWriter(IServiceProvider services, string fileName, 
                int batchSize = 100, TimerInterval interval = TimerInterval.T_500_Ms,
                Func<LogEntry, bool> filter = null,   string startMessage = null) {
      _logService = services.GetService<ILogService>(); 
      _fileName = fileName;
      _filter = filter;
      var timers = services.GetService<ITimerService>();
      _logSubscription = _logService.Subscribe(LogService_OnNext, LogService_OnCompleted);
      _buffer = new ActiveBatchingBuffer<LogEntry>(timers, batchSize, interval);
      _buffer.Subscribe(Buffer_OnBatchProduced);
      if(startMessage != null)
        WriteToFile(startMessage + Environment.NewLine); 
    }

    private void LogService_OnNext(LogEntry entry) {
      if(_filter == null || _filter(entry)) {
        _buffer.Push(entry);
        if (entry.IsError)
          Flush(); // error flush immediately
      }
    }
    private void LogService_OnCompleted() {
      _buffer.Flush(); 
    }

    private void Buffer_OnBatchProduced(IList<LogEntry> entries) {
      var strings = entries.Select(i => i.AsText()).ToArray();
      var text = string.Join(Environment.NewLine, strings);
      WriteToFile(text);
    }

    public void Flush() {
      _buffer.Flush(); 
    }

    private void WriteToFile(string text) {
      try {
        // We use AppendAllLines to ensure NewLine between chunks
        lock(_fileWriteLock)
          File.AppendAllLines(_fileName, new string[] { text });
      } catch(Exception ex) {
        LastResortErrorLog.Instance.LogFatalError(ex.ToLogString(), text);
      }
    }

     ~LogFileWriter() {
      if (!_disposed)
        Dispose();
      GC.SuppressFinalize(this);
    }

    public void Dispose() {
      if (_buffer.Count > 0)
        Flush();
      _disposed = true;
    }

  }
}
