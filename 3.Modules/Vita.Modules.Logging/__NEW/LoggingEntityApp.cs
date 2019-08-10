using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.DbInfo;
using Vita.Data.Upgrades;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {

  //An attempt to provide a pre-build app for logging that can run side-by-side with main app, and use different logging database
  public class LoggingEntityApp : EntityApp {
    public const string CurrentVersion = "1.1.0.0";

    //ErrorLog is available as property in base EntityApp class
    public readonly ILogService OperationLog;
    public readonly ITransactionLogService TransactionLog;
    public readonly IDbUpgradeLogService DbUpgradeLog;

    public LoggingEntityApp(string schema = "log") : base("LoggingApp", CurrentVersion) {
      var area = base.AddArea(schema);
      // DbInfo module is not shared with main app, it is local for the database
      var dbInfo = new DbInfoModule(area);
      // ErrorLog is property in EntityApp, will be set there automatically
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
