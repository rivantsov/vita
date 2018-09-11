using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public class LogFileWriter : IAsyncLogWriter, ILogListener, IDisposable {
    string _fileName;
    int _pauseMs;
    LogEntryType[] _entryTypesFilter; //filter by entry type; if null - all entry types

    ConcurrentQueue<LogEntry> _entries = new ConcurrentQueue<LogEntry>();
    object _flushLock = new object(); 
    static object _fileWriteLock = new object(); 

    public LogFileWriter(string fileName, int pauseMs = 100, LogEntryType[] entryTypesFilter = null) {
      _fileName = fileName;
      _pauseMs = pauseMs;
      if(entryTypesFilter != null && entryTypesFilter.Length > 0)
        _entryTypesFilter = entryTypesFilter;
    }

    public void Start(CancellationToken token) {
      Task.Run(() => StartAsync(token));
    }

    public void AddEntry(LogEntry entry) {
      if(CanWriteEntry(entry)) {
        _entries.Enqueue(entry);
        if(entry.EntryType == LogEntryType.Error)
          SaveAll();           
      }
    }

    // fast lookup
    private bool CanWriteEntry(LogEntry entry) {
      if(entry == null)
        return false; 
      if(_entryTypesFilter == null)
        return true;
      var type = entry.EntryType; 
      for(int i = 0; i < _entryTypesFilter.Length; i++)
        if(_entryTypesFilter[i] == type)
          return true;
      return false; 
    }

    public async Task StartAsync(CancellationToken token) {
      while(!token.IsCancellationRequested) {
        // Task.Delay throws exception on cancellation
        try {
          await Task.Delay(_pauseMs, token); //let entries accumulate
        } catch(TaskCanceledException) { }
        // even if cancellation requested (system shutdown), dump the queue
        SaveAll(); 
      }
    }//method

    private void SaveAll() {
      var list = new List<string>();
      lock(_flushLock) {
        while(_entries.TryDequeue(out LogEntry entry))
          list.Add(entry?.ToString());
      }
      if(list.Count == 0)
        return;
      // add separator
      list.Add(Environment.NewLine);
      // actually write to file
      var text = string.Join(Environment.NewLine, list);
      SafeWriteToFile(text);
    }

    private void SafeWriteToFile(string text) {
      try {
        lock(_fileWriteLock)
          File.AppendAllText(_fileName, text);
      } catch(Exception ex) {
        System.Diagnostics.Trace.WriteLine($"LOG failure, failed to write to file [{_fileName}], error: {ex.Message}");
      }

    }

    public void Dispose() {
      SaveAll();
    }
  }
}
