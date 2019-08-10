using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  public class LogFileWriter : ILogListener, IDisposable {
    string _fileName;

    BatchingQueue<LogEntry> _queue; 

    static object _fileWriteLock = new object(); 

    public LogFileWriter(string fileName, int batchSize = 1000, int maxLingerMs = 200, string startMessage = null) {
      _fileName = fileName;
      _queue = new BatchingQueue<LogEntry>(batchSize, maxLingerMs);
      _queue.Batched += Queue_Batched;
      if (startMessage != null)
        WriteToFile(startMessage + Environment.NewLine); 
      
    }

    private void Queue_Batched(object sender, QueueBatchEventArgs<LogEntry> e) {
      var strings = e.Items.Select(i => i.AsText()).ToList();
      strings.Add(Environment.NewLine);
      var text = string.Join(Environment.NewLine, strings);
      WriteToFile(text);
    }

    public void AddEntry(LogEntry entry) {
      if(entry != null) {
        _queue.Enqueue(entry);
        if (entry.EntryType == LogEntryType.Error)
          _queue.ProduceBatch();  // Force saving to file          
      }
    }

    public void Flush() {
      _queue.ProduceBatch();
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
      Dispose(); 
    }

    public void Dispose() {
      if (_queue.Count > 0)
        _queue.ProduceBatch(); 
    }

  }
}
