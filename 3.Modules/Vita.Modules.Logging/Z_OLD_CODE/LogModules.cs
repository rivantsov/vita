using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Logging {

  [Flags]
  public enum LogModules {
    None = 0, 
    ErrorLog = 1,
    UserSession = 1 << 1,
    OperationLog = 1 << 2,
    TransactionLog = 1 << 3,
    EventLog = 1 << 4, 
    IncidentLog = 1 << 5, 
    NotificationLog = 1 << 6, 
    DbUpgradeLog = 1 << 7,
    LoginLog = 1 << 8,
    WebCallLog = 1 << 9, 
    WebClientLog = 1 << 10, 

    All = ErrorLog | UserSession | OperationLog | TransactionLog | EventLog | IncidentLog 
        | NotificationLog | DbUpgradeLog | LoginLog | WebCallLog | WebClientLog
  }

  public static class LogModulesExtensions {
    public static bool IsSet(this LogModules modules, LogModules module) {
      return (modules & module) != 0; 
    } 
  }
}
