using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public interface ILog {
    void AddEntry(LogEntry entry);
  }

  public interface ILogListener : ILog { }

  public interface ILogService : ILog {
    void AddListener(ILogListener listener);
    void RemoveListener(ILogListener listener);
  }

  public interface IAsyncLogWriter {
    Task StartAsync(CancellationToken token);
    void AddEntry(LogEntry entry);
  }



}
