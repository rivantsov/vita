using System;
using System.Collections.Generic;
using System.Linq; 
using System.Text;

using Vita.Entities.Logging;
using Vita.Entities.Services.Implementations;
using Vita.Entities.Model.Construction;
using Vita.Entities.Utilities;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Entities.Services;
using Vita.Data.Runtime;
using System.Threading.Tasks;
using System.Threading;

namespace Vita.Entities {

  public partial class EntityApp {
    public static string LastFatalError; // error log sets this if it fails to persist error

    protected virtual void InitApp() {

      Status = EntityAppStatus.Initializing;
      CreateLogFileWriters();
      Log.WriteMessage("Initializing EntityApp {0}.====================================", this.AppName);
      this.AppEvents.OnInitializing(EntityAppInitStep.Initializing);
      //Check dependencies
      foreach(var mod in this.Modules) {
        var depList = mod.GetDependencies();
        foreach(var dep in depList) {
          var ok = Modules.Any(m => dep.IsTypeOrSubType(m));
          if(!ok)
            Log.LogError("Module {0} requires dependent module {1} which is not included in the app.", mod.GetType(), dep);
        }
      }
      Log.CheckErrors(); 
      // Init linked apps 
      foreach(var linkedApp in LinkedApps)
        if (linkedApp.Status == EntityAppStatus.Created)
        linkedApp.Init();

      //Build model
      var builder = new EntityModelBuilder(this);
      builder.BuildModel();
      Log.CheckErrors(); 
      //Notify modules that entity app is constructed
      foreach(var module in this.Modules)
        module.Init();
      //init services
      var servList = this.GetAllServices();
      for(int i = 0; i < servList.Count; i++) {
        var service = servList[i];
        var iServiceInit = service as IEntityServiceBase;
        if(iServiceInit != null)
          iServiceInit.Init(this);
      }
      //complete initialization
      this.AppEvents.OnInitializing(EntityAppInitStep.Initialized);
      foreach(var module in this.Modules)
        module.AppInitComplete();

      builder.CheckErrors();
      Status = EntityAppStatus.Initialized;
      Log.WriteMessage("App {0} initialized.", this.AppName);
    }

    protected void CreateLogFileWriters() {
      var logService = GetService<ILogService>();
      if(this.LogFileWriter == null && !string.IsNullOrEmpty(this.LogPath)) {
        var logWriter = new LogFileWriter(LogPath);
        LogFileWriter = logWriter; 
        logService.AddListener(logWriter);
        logWriter.Start(this.ShutdownToken);
      }
      if(this.ErrorLogFileWriter == null && !string.IsNullOrEmpty(this.ErrorLogPath)) {
        ErrorLogFileWriter = new LogFileWriter(ErrorLogPath);
        logService.AddListener(ErrorLogFileWriter, entry => entry.EntryType == LogEntryType.Error);
      }
    }

    protected virtual void ConnectToDatabase(DbSettings dbSettings) {
      switch(this.Status) {
        case EntityAppStatus.Created:
          this.Init();
          break;
        case EntityAppStatus.Shutdown:
          return;
      } 
      Log.WriteMessage("  Connecting to data source {0}.", dbSettings.DataSourceName);
      dbSettings.CheckConnectivity(rethrow: true);
      var dbModel = GetCreateDbModel(dbSettings, Log);
      var db = new Database(dbModel, dbSettings);      
      var ds = new DataSource(dbSettings.DataSourceName, db);
      this.DataAccess.RegisterDataSource(ds);
      this.DataSourceEvents.OnDataSourceChange(new DataSourceEventArgs(db, dbSettings.DataSourceName, DataSourceEventType.Connecting));
      CheckUpgradeDatabase(db);
      Log.Flush(); 
      this.Status = EntityAppStatus.Connected;
      this.DataSourceEvents.OnDataSourceChange(new DataSourceEventArgs(db, dbSettings.DataSourceName, DataSourceEventType.Connected));
      Log.WriteMessage("Connected to {0}.", dbSettings.DataSourceName);
      Log.Flush();
    }

    protected virtual DbModel GetCreateDbModel(DbSettings settings, ILog log) {
      DbModel dbModel = null; 
      //Check if model is shared
      var dbModelConfig = settings.ModelConfig;
      bool modelIsShared = dbModelConfig.Options.IsSet(DbOptions.ShareDbModel);
      lock(_lock) { //we need lock to prevent collision on shared model
        if(modelIsShared)
          dbModel = dbModelConfig.SharedDbModel;
        if(dbModel == null) {
          var dbmBuilder = new DbModelBuilder(Model, dbModelConfig, log);
          dbModel = dbmBuilder.Build();
          if(modelIsShared)
            dbModelConfig.SharedDbModel = dbModel;
        }
      }//lock
      return dbModel;      
    }//method

    public void CheckUpgradeDatabase(Database db) {
      //Invoke upgrade
      // Update db model
      if(db.Settings.UpgradeMode == DbUpgradeMode.Never) {
        Log.WriteMessage("Data source upgrade mode set to Never, skipping db upgrade.");
        return; 
      }
      // upgrade
      var updateMgr = new DbUpgradeManager(db, Log);
      var upgradeInfo = updateMgr.UpgradeDatabase(); //it might throw exception
      // _events.OnDataSourceStatusChanging(new DataSourceEventArgs(dataSource, DataSourceEventType.Connected));
    }


    long _lastTransactionId;
    public long GenerateNextTransactionId() {
      if (_lastTransactionId == 0) {
        var timeSince2k = DateTime.UtcNow.Subtract(new DateTime(2010, 1, 1));
        _lastTransactionId = (long) timeSince2k.TotalMilliseconds * 10;
      }
      var nextTransId = Interlocked.Increment(ref _lastTransactionId);
      return nextTransId; 
    }

  }//class
}
