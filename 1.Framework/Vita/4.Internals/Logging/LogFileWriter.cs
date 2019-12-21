using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using Vita.Entities.Utilities;
using Vita.Internals.Utilities;

namespace Vita.Entities.Logging {

  public class LogFileWriter : IDisposable {
    string _fileName;
    ILogService _logService;
    int _batchSize; 
    Func<LogEntry, bool> _filter;
    bool _disposed;

    LinkedQueue _queue; 
    static object _fileWriteLock = new object(); 

    public LogFileWriter(ILogService logService, string fileName, int batchSize = 100, 
                Func<LogEntry, bool> filter = null,   string startMessage = null) {
      _logService = logService; 
      _fileName = fileName;
      _filter = filter;
      _batchSize = batchSize;
      _logService.EntryAdded += LogService_EntryAdded;
      _logService.FlushRequested += LogService_FlushRequested;
      _queue = new LinkedQueue();
      if (startMessage != null)
        WriteToFile(startMessage + Environment.NewLine); 
    }

    private void LogService_FlushRequested(object sender, EventArgs e) {
      Flush(); 
    }

    private void LogService_EntryAdded(object sender, LogEntryEventArgs e) {
      if(_filter == null || _filter(e.Entry)) {
        _queue.EnqueueNode(e.Entry);
        if (e.Entry.EntryType == LogEntryType.Error)
          Flush(); // error flush immediately
      }
    }

    public void AddEntry(LogEntry entry) {
      if(entry != null) {
        _queue.EnqueueNode(entry);
      }
    }

    public void Flush() {
      var entries = _queue.DequeueNodes<LogEntry>();
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
      _logService.EntryAdded -= LogService_EntryAdded;
      _logService.FlushRequested -= LogService_FlushRequested;
      _disposed = true;
    }

  }
}
