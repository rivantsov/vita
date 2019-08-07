using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public class LogFileWriter : ILogListener, IDisposable {
    string _fileName;
    int _pauseMs;

    ConcurrentQueue<LogEntry> _entries = new ConcurrentQueue<LogEntry>();
    object _flushLock = new object(); 
    static object _fileWriteLock = new object(); 

    public LogFileWriter(string fileName, int pauseMs = 100) {
      _fileName = fileName;
      _pauseMs = pauseMs;
    }

    public void Start(CancellationToken token) {
      Task.Run(() => StartAsync(token));
    }

    public void AddEntry(LogEntry entry) {
      if(entry != null) {
        _entries.Enqueue(entry);
        if(entry.EntryType == LogEntryType.Error)
          SaveAll();           
      }
    }
    public void Flush() {
      SaveAll(); 
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
        LastResortErrorLog.Instance.LogFatalError(ex.ToLogString(), text);
      }

    }

     ~LogFileWriter() {
      Dispose(); 
    }

    public void Dispose() {
      if (_entries.Count > 0)
        SaveAll();
    }
  }
}
