using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Authorization;
using Vita.Modules.Logging.Api; 

namespace Vita.Modules.Logging {
  public class LoggingAuthorizationRoles {
    public readonly Role LogDataViewerRole;
    public readonly Activity ViewingLogs;
    public readonly ObjectAccessPermission ViewLogsPermission;
    // Power user role to access log information

    public static LoggingAuthorizationRoles Instance {
      get {
        if(_instance == null)
          _instance = new LoggingAuthorizationRoles();
        return _instance; 
      }
    } static LoggingAuthorizationRoles _instance; 

    //static constructor
    private LoggingAuthorizationRoles() {
      var loggingResources = new EntityGroupResource("Logs", typeof(IErrorLog), typeof(INotificationLog),
                  typeof(IDbUpgradeBatch), typeof(IDbUpgradeScript), typeof(IIncidentLog), typeof(IIncidentAlert),
                  typeof(ILoginLog), typeof(IOperationLog), typeof(ITransactionLog), typeof(IWebCallLog), 
                  typeof(IEvent), typeof(IEventParameter));
      var viewLogs = new EntityGroupPermission("ViewLogs", AccessType.Read, loggingResources);
      ViewingLogs = new Activity("ViewingLogs", viewLogs);
      ViewLogsPermission = new ObjectAccessPermission("ViewLogsPermission", AccessType.ApiGet, typeof(LoggingDataController));
      LogDataViewerRole = new Role("LogsAnalyzerRole", ViewingLogs);
      LogDataViewerRole.Grant(ViewLogsPermission);
    }
  
  }
}
