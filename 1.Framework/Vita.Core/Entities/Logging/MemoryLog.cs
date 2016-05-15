using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;

namespace Vita.Entities.Logging {

  public class MemoryLog {
    OperationContext _context; 
    public int MaxEntries;
    ConcurrentQueue<OperationLogEntry> _entries = new ConcurrentQueue<OperationLogEntry>();

    public MemoryLog(OperationContext context, int maxEntries = 200) {
      _context = context; 
      MaxEntries = maxEntries; 
    }

    public void AddEntry(OperationLogEntry entry) {
      var str = entry.ToString();
      if (str.StartsWith("-- BEGIN BATCH")) {

      }
      OperationLogEntry dummy;
      while(_entries.Count > MaxEntries)
        _entries.TryDequeue(out dummy); 
      _entries.Enqueue(entry); 
    }
    public void Info(string message, params object[] args) {
      AddEntry(new InfoLogEntry(_context, message, args));
    }
    public void Error(string message, params object[] args) {
      AddEntry(new ErrorLogEntry(_context, message, args));
    }

    public bool HasErrors() {
      return _entries.Any(e => e.EntryType == LogEntryType.Error);
    }

    public string GetAllAsText() {
      var ordered = _entries.ToArray().OrderBy(e => e.CreatedOn).Select(e => e.ToString()).ToArray(); 
      return string.Join(Environment.NewLine, ordered);
    }
    public void DumpTo(string fileName) {
      var text = GetAllAsText();
      try {
        System.IO.File.AppendAllText(fileName, text); 
      } catch (Exception ex) {
        System.Diagnostics.Trace.WriteLine("Failed to dump log to file " + fileName + "'; error: " + ex.ToLogString());
      }
    }
  }


  /* Notes on logging
   Logging scenarios:
1. Simple, standalone log accumulating log entries, not writing them to any persistent storage. Accumulated log entries are available 'upon request'. 
   Example: local session log associated with OperationContext. All SQL statements, comments and messages and written to this log. 
   In case of exception the log contents are saved with exception  information; but otherwise, if session ends without any errors, log is discarded. 
2. Long-running log that regularly persists its content to a file or a database.  
   Example: IOperationLogService accumulates all messages from all running user sessions. It contains all SQL statements executed in the database. 

The main concern is impact on performance - avoiding slowing down the main execution thread by logging activities. 
Time consuming activities which are points of concern: 
  a. Formatting log entries
  b. Persisting the formatted entries (plain text)
  
The solution
To mitigate point 'a' (formatting), we do not format entries into text immediately. Log is implemented as a list/stream of objects containing raw information: 
a message template with arguments array, or DbCommand for db log entries. The entries are formatted into text on a background thread when persisting the log. 
For point b: First, entries are accumulated in chunks and persisted in chunks together. Secondly, the persisting activity 
is delegated to a background thread, which wakes up by a timer and persists all accumulated entries. Entries formatting is also done on a background thread, right when we persist them.   

 
   */
}
