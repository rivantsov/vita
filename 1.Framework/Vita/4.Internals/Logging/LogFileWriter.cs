using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  public class LogFileWriter : IDisposable {
    string _fileName;
    ILogService _logService;
    int _batchSize; 
    Func<LogEntry, bool> _filter;
    IDisposable _logSubscription; 
    bool _disposed;

    BatchingQueue<LogEntry> _queue = new BatchingQueue<LogEntry>(); 
    static object _fileWriteLock = new object(); 

    public LogFileWriter(ILogService logService, string fileName, int batchSize = 100, 
                Func<LogEntry, bool> filter = null,   string startMessage = null) {
      _logService = logService; 
      _fileName = fileName;
      _filter = filter;
      _batchSize = batchSize;
      _logSubscription = _logService.Subscribe(ObserverHelper.FromHandlers<LogEntry>(LogService_OnNext, LogService_OnCompleted));
      if (startMessage != null)
        WriteToFile(startMessage + Environment.NewLine); 
    }

    private void LogService_OnCompleted() {
      Flush(); 
    }

    private void LogService_OnNext(LogEntry entry) {
      if(_filter == null || _filter(entry)) {
        _queue.Enqueue(entry);
        if (entry.IsError)
          Flush(); // error flush immediately
      }
    }

    public void AddEntry(LogEntry entry) {
      if(entry != null) {
        _queue.Enqueue(entry);
      }
    }

    public void Flush() {
      var entries = _queue.DequeueMany();
      var strings =  entries.Select(i => i.AsText()).ToList();
      strings.Add(Environment.NewLine);
      var text = string.Join(Environment.NewLine, strings);
      WriteToFile(text);
    }

    private void WriteToFile(string text) {
      try {
        lock(_fileWriteLock)
          File.AppendAllText(_fileName, text);
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
      if (_queue.Count > 0)
        Flush();
      _disposed = true;
    }

  }
}
