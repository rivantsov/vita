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

    protected virtual void InitApp() {

      Status = EntityAppStatus.Initializing;
      SetupLogFileWriters();
      ActivationLog.WriteMessage("Initializing EntityApp {0}.====================================", this.AppName);
      this.AppEvents.OnInitializing(EntityAppInitStep.Initializing);
      //Check dependencies
      foreach(var mod in this.Modules) {
        var depList = mod.GetDependencies();
        foreach(var dep in depList) {
          var ok = Modules.Any(m => dep.IsTypeOrSubType(m));
          if(!ok)
            ActivationLog.LogError($"Module {mod.Name} requires dependent module {dep} which is not included in the app.");
        }
      }
      CheckActivationErrors(); 

      //Build model
      var builder = new EntityModelBuilder(this);
      builder.BuildModel();
      CheckActivationErrors();

      //Notify modules that entity app is constructed
      foreach (var module in this.Modules)
        module.Init();
      //init services; note that service might be registered more than once, under different interface
      var servList = this.GetAllServices().Distinct().ToList();
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
      ActivationLog.WriteMessage("App {0} initialized.", this.AppName);
    }

    private void CheckActivationErrors() {
      ActivationLog.CheckErrors("Application initialization failed.");
    }

    protected void SetupLogFileWriters() {
      if (!string.IsNullOrEmpty(this.LogPath)) {
        LogFileWriter = new LogFileWriter(LogPath, 
          startMessage: $" ======== Log Started {this.TimeService.UtcNow} ============");
      }
      if(!string.IsNullOrEmpty(this.ErrorLogPath)) {
        ErrorLogFileWriter = new LogFileWriter(ErrorLogPath, 
          startMessage: $"==== Error Log Started {this.TimeService.UtcNow} ===========");
      }
    }

    protected virtual void ConnectToDatabase(DbSettings dbSettings) {
      try {
        switch(this.Status) {
          case EntityAppStatus.Created:
            this.Init();
            break;
          case EntityAppStatus.Shutdown:
            return;
        }
        ActivationLog.WriteMessage("  Connecting to data source {0}.", dbSettings.DataSourceName);
        dbSettings.CheckConnectivity(rethrow: true);
        var dbModel = GetCreateDbModel(dbSettings);
        var db = new Database(dbModel, dbSettings);
        var ds = new DataSource(dbSettings.DataSourceName, db);
        this.DataAccess.RegisterDataSource(ds);
        this.DataSourceEvents.OnDataSourceChange(new DataSourceEventArgs(db, dbSettings.DataSourceName, DataSourceEventType.Connecting));
        CheckUpgradeDatabase(db);
        LogService.Flush();
        this.Status = EntityAppStatus.Connected;
        this.AppEvents.OnConnected(dbSettings.DataSourceName);
        this.DataSourceEvents.OnDataSourceChange(new DataSourceEventArgs(db, dbSettings.DataSourceName, DataSourceEventType.Connected));
        ActivationLog.WriteMessage("Connected to {0}.", dbSettings.DataSourceName);
      } finally {
        LogService.Flush();
      }
    }

    protected virtual DbModel GetCreateDbModel(DbSettings settings) {
      DbModel dbModel = null; 
      //Check if model is shared
      var dbModelConfig = settings.ModelConfig;
      bool modelIsShared = dbModelConfig.Options.IsSet(DbOptions.ShareDbModel);
      lock(_lock) { //we need lock to prevent collision on shared model
        if(modelIsShared)
          dbModel = dbModelConfig.SharedDbModel;
        if(dbModel == null) {
          var dbmBuilder = new DbModelBuilder(Model, dbModelConfig, ActivationLog);
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
        ActivationLog.WriteMessage("Data source upgrade mode set to Never, skipping db upgrade.");
        return; 
      }
      // upgrade
      var updateMgr = new DbUpgradeManager(db, ActivationLog);
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
