using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public interface ILog {
    void AddEntry(LogEntry entry);
  }

  public interface IBufferedLog : ILog {
    IList<LogEntry> GetAll();
    int ErrorCount { get; }
  }

  public interface ILogService : ILog, IObservable<LogEntry> {
    void Flush(); 
  }

  /// <summary>The last-resort error log facility. <see cref="LastResortErrorLog" class is an implementation./> </summary>
  public interface ILastResortErrorLog {
    /// <summary> Logs a fatal error in logging system. </summary>
    void LogFatalError(string logSystemError, string originalError = null);
  }


}
