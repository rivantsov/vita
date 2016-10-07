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
    //ErrorLog is available as property in base EntityApp class
    public readonly IOperationLogService OperationLog;
    public readonly IIncidentLogService IncidentLog;
    public readonly ITransactionLogService TransactionLog;
    public readonly IWebCallLogService WebCallLog;
    public readonly INotificationLogService NotificationLog;
    public readonly ILoginLogService LoginLog;
    public readonly IDbUpgradeLogService DbModelChangeLog;
    public readonly IUserSessionService SessionService;
    public readonly IDbInfoService DbInfoService;
    public readonly IEventLogService EventLogService;
    public readonly IWebClientLogService WebClientLogService;

    private TransactionLogModule _transactionLogModule;

    public LoggingEntityApp(string schema = "log", UserSessionSettings sessionSettings = null) : base("LoggingApp", CurrentVersion) {
      var area = base.AddArea(schema);
      var errorLog = new ErrorLogModule(area);
      OperationLog = new OperationLogModule(area);
      IncidentLog = new IncidentLogModule(area);
      TransactionLog = _transactionLogModule = new TransactionLogModule(area, trackHostApp: false); //do not track changes for LoggingApp itself
      WebCallLog = new WebCallLogModule(area);
      NotificationLog = new NotificationLogModule(area);
      LoginLog = new LoginLogModule(area);
      DbModelChangeLog = new DbUpgradeLogModule(area);
      SessionService = new UserSessionModule(area, sessionSettings);
      DbInfoService = new DbInfoModule(area);
      EventLogService = new EventLogModule(area);
      WebClientLogService = new WebClientLogModule(area); 
    }

    public void LinkTo(EntityApp mainApp) {
      Util.Check(mainApp.Status == EntityAppStatus.Created, "Invalid main app status, should be Created. " + 
                     "Call LoggingEntityApp.LinkTo(mainApp) immediately after creating the main app instance.");
      mainApp.LinkedApps.Add(this);
      // make sure time service is created, so MainApp imports it on initialization, so 2 apps share the same time service
      this.RegisterService<ITimeService>(new Vita.Entities.Services.Implementations.TimeService());
      // We do not import logging services directly into the main app - they will be found automatically; GetService checks for services in linked apps 
      // Tell transacton log to hookup to the main app
      _transactionLogModule.SetupUpdateLoggingFor(mainApp);
    }

    public IDisposable SuspendLogging() {
      var saveService = this.GetService<IBackgroundSaveService>();
      return saveService.Suspend(); 
    }

  }//class
}
