using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.DbInfo;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging {

  //An attempt to provide a pre-build app for logging that can run side-by-side with main app, and use different logging database
  public class LoggingEntityApp : EntityApp {
    public const string CurrentVersion = "1.1.0.0";

    public ErrorLogModule ErrorLog;
    LogPersistenceService _logBatchingService; 

    public LoggingEntityApp(string schema = "log") : base("LoggingEntityApp", CurrentVersion) {
      var area = base.AddArea(schema);
      ErrorLog = new ErrorLogModule(area); 
    }

    public void ListenTo(EntityApp targetApp) {
      // Hook to target log service
      var targetLogService = targetApp.GetService<ILogService>();
      targetLogService.Subscribe(OnLogEntryAdded, OnLogServiceOnCompleted);
    }

    public void OnLogEntryAdded(LogEntry entry) {
      _logBatchingService.Push(entry); 
    }
    public void OnLogServiceOnCompleted() {
      _logBatchingService.Flush();
    }

  }//class
}
