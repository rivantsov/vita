using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Entities.Services {

  public enum LogLevel {
    None,
    Basic,   // messages only, no SQL log; for web: URL, time, controller/method name
    Details, // messages, SQL, web request/response bodies, headers
  }

  public interface IOperationLogService {
    LogLevel LogLevel { get; }
    void Log(LogEntry entry);
  }

}
