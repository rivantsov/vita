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

    public LogEntityModule LogModule;
    public DbInfoModule DbInfoModule; 

    //ErrorLog is available as property in base EntityApp class
    public readonly ILogService OperationLog;
    public readonly IDbUpgradeLogService DbUpgradeLog;
    public readonly LogServiceListener LogListener; 

    public LoggingEntityApp(string schema = "log") : base("LoggingApp", CurrentVersion) {
      var area = base.AddArea(schema);
      // DbInfo module is not shared with main app, it is local for the database
      DbInfoModule = new DbInfoModule(area);
      LogModule = new LogEntityModule(area);
      LogListener = new LogServiceListener(this);
    }

    public void LinkTo(EntityApp mainApp) {
      Util.Check(mainApp.Status == EntityAppStatus.Created, "Invalid target/main app status: {0}, should be Created. " + 
                     "Call LoggingEntityApp.LinkTo(mainApp) immediately after creating the main app instance.", mainApp.Status);
      mainApp.LinkedApps.Add(this);
      var targetLogService = mainApp.GetService<ILogService>();
      targetLogService.AddListener(this.LogListener);
    }


  }//class
}
