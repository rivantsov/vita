using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Services;
using Vita.Data.Model;
using Vita.Data.Driver;

namespace Vita.Data.Upgrades {

  public class DbUpgradeManager {
    EntityApp _app; 
    DataAccessService _dataAccess; 
    DataSource _dataSource;
    Database _database;
    MemoryLog _log;
    ITimeService _timeService;
    DbUpgradeInfo _upgradeInfo;

    public DbUpgradeManager(DataSource dataSource) {
      _dataSource = dataSource; 
      _dataAccess = (DataAccessService) _dataSource.App.DataAccess;
      _database = _dataSource.Database;
      _app = _database.DbModel.EntityApp; 
      _log = _app.ActivationLog;
      _timeService = _app.GetService<ITimeService>();
    }

    public DbUpgradeInfo UpgradeDatabase() {
      //Check db version vs app version
      _upgradeInfo = BuildUpgradeInfo();
      if(_upgradeInfo.Status != UpgradeStatus.ChangesDetected) 
        return _upgradeInfo;
      ApplyUpgrades();
      _upgradeInfo.Status = UpgradeStatus.Applied;
      //TODO: review this and refactor
      var actLogFile = _database.DbModel.EntityApp.ActivationLogPath;
      if (!string.IsNullOrEmpty(actLogFile))
        _log.DumpTo(actLogFile);
      return _upgradeInfo; 

    }

    /// <summary>
    /// Performs Db model update actions, depending on instance type (dev, staging, production). 
    /// </summary>
    public DbUpgradeInfo BuildUpgradeInfo() {
      //Even if we do not do db model upgrades, we still may have migration actions (post upgrate), which we will run after completing connection to db
      var driver = _database.DbModel.Driver;
      var loader = driver.CreateDbModelLoader(_database.Settings, _log);
      _upgradeInfo = new DbUpgradeInfo(_database.Settings, _database.DbModel);
      var oldDbVersion = LoadDbVersionInfo(loader);
      if (!CheckCanUpgrade(oldDbVersion)) 
        return _upgradeInfo; 
      _upgradeInfo.OldDbModel = loader.LoadModel();
      _upgradeInfo.OldDbModel.VersionInfo = oldDbVersion;
      //assign prior versions 
      //Compare two models and get changes
      var modelComparer = new DbModelComparer();
      modelComparer.AddDbModelChanges(_upgradeInfo, _log);
      //build scripts
      var updater = driver.CreateDbModelUpdater(_database.Settings);
      updater.BuildScripts(_upgradeInfo);
      //Add migrations
      var migrSet = new DbMigrationSet(_app, _database, _upgradeInfo.OldDbModel);
      foreach (var module in this._database.DbModel.EntityApp.Modules) {
        migrSet.CurrentModule = module;
        module.RegisterMigrations(migrSet);
      }
      migrSet.CurrentModule = null;
      _upgradeInfo.AddMigrations(migrSet);
      //Update final status
      _upgradeInfo.VersionsChanged = _database.DbModel.VersionInfo.VersionChanged(oldDbVersion);
      if (_upgradeInfo.AllScripts.Count > 0 || _upgradeInfo.VersionsChanged)
        _upgradeInfo.Status = UpgradeStatus.ChangesDetected;
      else
        _upgradeInfo.Status = UpgradeStatus.NoChanges; 
      //Sort, Clear up
      _upgradeInfo.AllScripts.Sort(DbUpgradeScript.CompareExecutionOrder);
      _database.DbModel.ResetPeerRefs(); //drop refs to old model
      return _upgradeInfo;
    }

    public DbVersionInfo LoadDbVersionInfo(DbModelLoader loader) {
      var infoProvider = _database.Settings.DbInfoProvider;
      if (infoProvider == null)
        return null; 
      var versionInfo = infoProvider.LoadDbInfo(_database.Settings, _app.AppName, loader);
      loader.VersionInfo = versionInfo; 
      return versionInfo;
    }

    private bool CheckCanUpgrade(DbVersionInfo oldDbVersion) {
      var appVersion = _database.DbModel.EntityApp.Version;
      if (oldDbVersion != null && oldDbVersion.Version > appVersion) {
        _upgradeInfo.Status = UpgradeStatus.HigherVersionDetected;
        OnHigherVersionDetected();
        var error = StringHelper.SafeFormat(
          "Downgrade detected: database version ({0}) is higher than EntityApp version ({1}). Activation canceled.", oldDbVersion.Version, appVersion);
        _log.Error(error);
        return false; 
      }
      switch (_database.Settings.UpgradeMode) {
        case DbUpgradeMode.Always:
          return true;
        case DbUpgradeMode.NonProductionOnly:
          if (oldDbVersion != null && oldDbVersion.InstanceType == DbInstanceType.Production) {
            _upgradeInfo.Status = UpgradeStatus.NotAllowed;
            return false; 
          }
          return true; 
        case DbUpgradeMode.Never:
        default:
          _upgradeInfo.Status = UpgradeStatus.NotAllowed;
          return false; 
      }
    }

    public void ApplyUpgrades() {
      if (_log != null)
        _log.Info("Applying DB Upgrade     ================================================================");
      OnUpgrading(_timeService.UtcNow);
      var startedOn = _timeService.UtcNow;
      var driver = _database.DbModel.Driver;
      var conn = driver.CreateConnection(_database.Settings.SchemaManagementConnectionString);
      DbUpgradeScript currScript = null;
      IDbCommand cmd = null; 
      var appliedScripts = new List<DbUpgradeScript>(); 
      try {
        conn.Open();
        foreach(var script in _upgradeInfo.AllScripts) {
          currScript = script; 
          cmd = conn.CreateCommand();
          cmd.CommandText = script.Sql;
          var start = _timeService.ElapsedMilliseconds;
          driver.ExecuteCommand(cmd, DbExecutionType.NonQuery);
          script.Duration = (int)(_timeService.ElapsedMilliseconds - start);
          appliedScripts.Add(script);
          //Important for SQLite
          cmd.Connection = null;
          cmd.Dispose();
          cmd = null;
        }
        if (_log != null) {
          _log.Info(_upgradeInfo.AllScripts.GetAllAsText());
          _log.Info("End DB Upgrade scripts  ================================================================");
        }
        // upgrade version numbers;
        SaveDbInfo();
        OnUpgraded(_upgradeInfo.AllScripts, startedOn, _timeService.UtcNow);
      } catch (Exception ex) {
        SaveDbInfo(ex);
        OnUpgraded(appliedScripts, startedOn, _timeService.UtcNow, ex, currScript);
        var logStr = ex.ToLogString();
        System.Diagnostics.Debug.WriteLine(logStr);
        _log.Error(logStr);
        throw;
      } finally {
        if(cmd != null) {
          cmd.Connection = null;
          cmd.Dispose();
        }
        conn.Close();
      }
    }//method


    public bool SaveDbInfo(Exception exception = null) {
      //Save if DbInfo service is available
      var infoService = _database.Settings.DbInfoProvider ?? _app.GetService<IDbInfoService>();
      if(infoService == null)
        return false;
      return infoService.UpdateDbInfo(_database, exception); //it never throws 
    }

    private void OnHigherVersionDetected() {
      var args = new DataSourceEventArgs(_dataSource, DataSourceEventType.HigherVersionDetected);
      _dataAccess.Events.OnDataSourceStatusChanging(args);
    }


    private void OnUpgrading(DateTime startingOn) {
      var args = new DbUpgradeEventArgs(_dataSource, DataSourceEventType.DbModelUpdating, _upgradeInfo.AllScripts, startingOn, null);
      _dataAccess.Events.OnDbUpgrading(args);
    }

    private void OnUpgraded(List<DbUpgradeScript> appliedScripts, 
                                 DateTime startedOn, DateTime completedOn,
                                 Exception exception = null, DbUpgradeScript failedScript = null) {
      var args = new DbUpgradeEventArgs(_dataSource, DataSourceEventType.DbModelUpdated, appliedScripts, 
                                            startedOn, completedOn, exception, failedScript);
      _dataAccess.Events.OnDbUpgraded(args);
      var logService = _app.GetService<IDbUpgradeLogService>();
      if(logService != null) {
        var oldVersionInfo = _upgradeInfo.OldDbModel.VersionInfo;
        var oldVersion = oldVersionInfo == null ? DbVersionInfo.ZeroVersion : oldVersionInfo.Version;
        var batch = new DbUpgradeReport() { Version = _database.DbModel.VersionInfo.Version, OldDbVersion = oldVersion, 
          MachineName = Environment.MachineName,  UserName = Environment.UserName, 
          Scripts = appliedScripts, StartedOn = startedOn, CompletedOn = completedOn, Exception = exception, FailedScript = failedScript };
        logService.LogDbUpgrade(batch);
      }
    }


  }//class
}
