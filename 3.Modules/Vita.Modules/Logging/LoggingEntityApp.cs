using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Web;
using Vita.Data.Upgrades;
using Vita.Modules.DbInfo;
using Vita.Common;

namespace Vita.Modules.Logging {

  //An attempt to provide a pre-build app for logging that can run side-by-side with main app, and use different logging database
  public class LoggingEntityApp : EntityApp {
    public const string CurrentVersion = "1.1.0.0";

    public readonly LogModules ActiveModules;
    //ErrorLog is available as property in base EntityApp class
    public readonly IOperationLogService OperationLog;
    public readonly IIncidentLogService IncidentLog;
    public readonly ITransactionLogService TransactionLog;
    public readonly IWebCallLogService WebCallLog;
    public readonly INotificationLogService NotificationLog;
    public readonly ILoginLogService LoginLog;
    public readonly IDbUpgradeLogService DbUpgradeLog;
    public readonly IUserSessionService SessionService;
    public readonly IEventLogService EventLogService;
    public readonly IWebClientLogService WebClientLogService;

    public LoggingEntityApp(string schema = "log", LogModules includeModule = LogModules.All, 
                            UserSessionSettings sessionSettings = null) : base("LoggingApp", CurrentVersion) {
      var area = base.AddArea(schema);
      ActiveModules = includeModule;
      // DbInfo module is not shared with main app, it is local for the database
      var dbInfo = new DbInfoModule(area);
      // ErrorLog is property in EntityApp, will be set there automatically
      if(ActiveModules.IsSet(LogModules.ErrorLog)) {
        var errLog = new ErrorLogModule(area);
      }
      if(ActiveModules.IsSet(LogModules.OperationLog))
        OperationLog = new OperationLogModule(area);
      if(ActiveModules.IsSet(LogModules.IncidentLog))
        IncidentLog = new IncidentLogModule(area);
      if(ActiveModules.IsSet(LogModules.TransactionLog))
        TransactionLog = new TransactionLogModule(area, trackHostApp: false); //do not track changes for LoggingApp itself
      if(ActiveModules.IsSet(LogModules.NotificationLog))
        NotificationLog = new NotificationLogModule(area);
      if(ActiveModules.IsSet(LogModules.LoginLog))
        LoginLog = new LoginLogModule(area);
      if(ActiveModules.IsSet(LogModules.DbUpgradeLog))
        DbUpgradeLog = new DbUpgradeLogModule(area);
      if(ActiveModules.IsSet(LogModules.UserSession))
        SessionService = new UserSessionModule(area, sessionSettings);
      if(ActiveModules.IsSet(LogModules.EventLog))
        EventLogService = new EventLogModule(area);
      if(ActiveModules.IsSet(LogModules.WebCallLog))
        WebCallLog = new WebCallLogModule(area);
      if(ActiveModules.IsSet(LogModules.WebClientLog))
        WebClientLogService = new WebClientLogModule(area);
    }

    public void LinkTo(EntityApp mainApp) {
      Util.Check(mainApp.Status == EntityAppStatus.Created, "Invalid target/main app status: {0}, should be Created. " + 
                     "Call LoggingEntityApp.LinkTo(mainApp) immediately after creating the main app instance.", mainApp.Status);
      mainApp.LinkedApps.Add(this);
      // We do not import logging services directly into the main app - they will be found automatically; GetService checks for services in linked apps 
      // Tell transacton log to hookup to the main app
      if (TransactionLog != null)
        TransactionLog.SetupLoggingFor(mainApp);
    }

    public IDisposable SuspendLogging() {
      var saveService = this.GetService<IBackgroundSaveService>();
      return saveService.Suspend(); 
    }

  }//class
}
