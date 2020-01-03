using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.DbInfo;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging.Db {

  /// <summary> A pre-build app for logging to database, the app runs side-by-side with main app
  /// and uses a separate logging database. </summary>
  public class DbLoggingEntityApp : EntityApp {
    public const string CurrentVersion = "2.0.0.0";

    public ErrorLogModule ErrorPersistenceModule; //saving errors, immediately
    public ILogPersistenceService PersistenceService; // batching and saving in batches
    public AppEventLogModule AppEventModule;
    public WebCallLogModule WebCallLog; 

    public DbLoggingEntityApp(string schema = "log") : base("LoggingEntityApp", CurrentVersion) {
      var area = base.AddArea(schema);
      ErrorPersistenceModule = new ErrorLogModule(area);
      PersistenceService = new LogPersistenceService();
      RegisterService<ILogPersistenceService>(PersistenceService);
      AppEventModule = new AppEventLogModule(area);
      WebCallLog = new WebCallLogModule(area); 
    }

    public void ListenTo(EntityApp targetApp) {
      // Hook to target log batch service - it will broadcast log batches, which this service will persist. 
      var targetLogBatchService = LogBatchingService.GetCreateLogBatchingService(targetApp);
      targetLogBatchService.Subscribe(PersistenceService);
      // Error log is different - we want to save errors immediately, without going thru batches, 
      // so we listen to general log and catch all error entries. 
      targetApp.LogService.Subscribe(ErrorPersistenceModule);
    }

  }//class
}
