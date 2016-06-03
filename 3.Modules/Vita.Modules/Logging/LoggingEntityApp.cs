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

    public EntityApp MainApp { get; private set; } //owner, 
    private TransactionLogModule _transactionLogModule;

    public LoggingEntityApp(string schema = "log", UserSessionSettings sessionSettings = null) : base("LoggingApp", CurrentVersion) {
      var area = base.AddArea(schema);
      var errorLog = new ErrorLogModule(area);
      OperationLog = new OperationLogModule(area);
      IncidentLog = new IncidentLogModule(area);
      TransactionLog = _transactionLogModule = new TransactionLogModule(area);
      WebCallLog = new WebCallLogModule(area);
      NotificationLog = new NotificationLogModule(area);
      LoginLog = new LoginLogModule(area);
      DbModelChangeLog = new DbUpgradeLogModule(area);
      SessionService = new UserSessionModule(area, sessionSettings);
      DbInfoService = new DbInfoModule(area);
      EventLogService = new EventLogModule(area); 
    }

    public void LinkTo(EntityApp mainApp) {
      MainApp = mainApp; 
      MainApp.LinkedApps.Add(this);
      MainApp.ImportServices(this, typeof(IErrorLogService), typeof(IOperationLogService), typeof(IIncidentLogService),
                                   typeof(ITransactionLogService), typeof(IWebCallLogService), typeof(INotificationLogService), 
                                   typeof(ILoginLogService), typeof(IDbUpgradeLogService), typeof(IUserSessionService), 
                                   typeof(IEventLogService)
                                   );
      _transactionLogModule.TargetApp = MainApp;
      //Replace time service with the instance from the main app
      base.TimeService = MainApp.TimeService;
      this.ImportServices(MainApp, typeof(ITimeService));
    }

    public IDisposable Suspend() {
      var saveService = this.GetService<IBackgroundSaveService>();
      return saveService.Suspend(); 
    }
  }//class
}
