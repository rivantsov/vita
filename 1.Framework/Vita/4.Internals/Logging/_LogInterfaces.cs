using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public interface ILog {
    void AddEntry(LogEntry entry);
  }

  public class LogEntryEventArgs: EventArgs {
    public readonly LogEntry Entry; 
    public LogEntryEventArgs(LogEntry entry) {
      Entry = entry; 
    }
  }

  public interface ILogService : ILog {
    event EventHandler<LogEntryEventArgs> EntryAdded;
    event EventHandler FlushRequested; 
    void Flush(); 
  }

  public interface IBufferedLog : ILog {
    IList<LogEntry> GetAll();
    int ErrorCount { get; }
  }


}
