using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Services;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.DbInfo;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;
using Vita.Data.Runtime;

namespace Vita.Data.Upgrades {

  public class DbUpgradeManager {
    EntityApp _app; 
    Database _database;
    ILog _log;
    IDbInfoService _dbInfoService; 
    DbUpgradeInfo _upgradeInfo;

    public DbUpgradeManager(Database database, ILog log) {
      _database = database;
      _log = log;
      _app = _database.DbModel.EntityApp; 
      _dbInfoService = (IDbInfoService) _app.GetService(typeof(IDbInfoService));
    }

    public DbUpgradeInfo UpgradeDatabase() {
      //Check db version vs app version
      _upgradeInfo = BuildUpgradeInfo();
      try {
        if(_upgradeInfo.Status == UpgradeStatus.ChangesDetected) {
          ApplyUpgrades();
          _upgradeInfo.Status = UpgradeStatus.Applied;
        }
        ApplyPostUpgradeMigrations();
        SaveDbVersionInfo();
      } catch(Exception ex) {
        SaveDbVersionInfo(ex);
        throw; 
      }
      return _upgradeInfo; 
    }

    private void ApplyPostUpgradeMigrations() {
      if(_upgradeInfo.PostUpgradeMigrations == null || _upgradeInfo.PostUpgradeMigrations.Count == 0)
        return;
      _app.DataSourceEvents.OnDbUpgrading(new DbUpgradeEventArgs(_database, DbUpgradeEventType.MigrationScriptsApplying, _upgradeInfo));
      var session = (EntitySession) _app.OpenSystemSession();
      // set explicit, admin-level connection
      session.CurrentConnection = new DataConnection(session, _database.Settings, DbConnectionLifetime.Operation, admin: true);
      foreach(var m in _upgradeInfo.PostUpgradeMigrations) {
        m.Action(session);
      }
      session.SaveChanges();
      _app.DataSourceEvents.OnDbUpgraded(new DbUpgradeEventArgs(_database, DbUpgradeEventType.MigrationScriptsApplying, null));
    }


    /// <summary>
    /// Performs Db model update actions, depending on instance type (dev, staging, production). 
    /// </summary>
    public DbUpgradeInfo BuildUpgradeInfo() {
      //Even if we do not do db model upgrades, we still may have migration actions (post upgrate), which we will run after completing connection to db
      var driver = _database.DbModel.Driver;
      _upgradeInfo = new DbUpgradeInfo(_database.Settings, _database.DbModel);
      var oldDbVersion = LoadDbVersionInfo();
      if (!CheckCanUpgrade(oldDbVersion)) 
        return _upgradeInfo;
      var loader = driver.CreateDbModelLoader(_database.Settings, _log);
      if (driver.Supports(DbFeatures.Schemas)) {
        var schemas = _database.DbModel.Schemas.Select(s => s.Schema).ToList();
        loader.SetSchemasSubset(schemas);
      }
      _upgradeInfo.OldDbModel = loader.LoadModel();
      _upgradeInfo.OldDbModel.VersionInfo = oldDbVersion;
      //Collect migrations; do it first, migr methods may add to ignore objects list.
      var migrSet = new DbMigrationSet(_app, _database, _upgradeInfo.OldDbModel);
      foreach(var module in this._database.DbModel.EntityApp.Modules) {
        migrSet.CurrentModule = module;
        module.RegisterMigrations(migrSet);
      }
      migrSet.CurrentModule = null;

      //assign prior versions 
      //Compare two models and get changes
      var modelComparer = new DbModelComparer();
      modelComparer.BuildDbModelChanges(_upgradeInfo, loader as IDbObjectComparer, _log);
      //build scripts
      var updater = driver.CreateDbModelUpdater(_database.Settings);
      updater.BuildScripts(_upgradeInfo);

      //Add migrations
      _upgradeInfo.AddMigrations(migrSet);
      //Update final status
      _upgradeInfo.VersionsChanged = oldDbVersion != null &&  _database.DbModel.VersionInfo.VersionChanged(oldDbVersion);
      if (_upgradeInfo.AllScripts.Count > 0 || _upgradeInfo.VersionsChanged)
        _upgradeInfo.Status = UpgradeStatus.ChangesDetected;
      else
        _upgradeInfo.Status = UpgradeStatus.NoChanges; 
      //Sort, Clear up
      _upgradeInfo.AllScripts.Sort(DbUpgradeScript.CompareExecutionOrder);
      _database.DbModel.ResetPeerRefs(); //drop refs to old model
      return _upgradeInfo;
    }

    public DbVersionInfo LoadDbVersionInfo() {
      if (_dbInfoService == null)
        return null; 
      var versionInfo = _dbInfoService.LoadDbVersionInfo(_database.DbModel, _database.Settings, _log);
      return versionInfo;
    }
    public bool SaveDbVersionInfo(Exception exception = null) {
      //Save if DbInfo service is available
      if(_dbInfoService == null)
        return false;
      return _dbInfoService.UpdateDbInfo(_database.DbModel, _database.Settings, _log, exception); //it never throws 
    }


    private bool CheckCanUpgrade(DbVersionInfo oldDbVersion) {
      var appVersion = _database.DbModel.EntityApp.Version;
      if (oldDbVersion != null && oldDbVersion.Version > appVersion) {
        _upgradeInfo.Status = UpgradeStatus.HigherVersionDetected;
        OnHigherVersionDetected();
        var error = Util.SafeFormat(
          "Downgrade detected: database version ({0}) is higher than EntityApp version ({1}). Activation canceled.", oldDbVersion.Version, appVersion);
        _log.LogError(error);
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
      OnUpgrading();
      var scriptsStr = string.Join(Environment.NewLine, _upgradeInfo.AllScripts);
      _log.WriteMessage("Applying DB Upgrades, {0} scripts. -----------------------------", _upgradeInfo.AllScripts.Count);
      _log.WriteMessage(scriptsStr);
      _upgradeInfo.StartedOn = _app.TimeService.UtcNow;
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
          var start = _app.TimeService.ElapsedMilliseconds;
          driver.ExecuteCommand(cmd, DbExecutionType.NonQuery);
          script.Duration = (int)(_app.TimeService.ElapsedMilliseconds - start);
          appliedScripts.Add(script);
          //Important for SQLite
          cmd.Connection = null;
          cmd.Dispose();
          cmd = null;
        }
        _upgradeInfo.EndedOn = _app.TimeService.UtcNow; 
        OnUpgraded(_upgradeInfo.AllScripts);
        _log.WriteMessage("Database upgraded successfully. ---------------------------");
      } catch (Exception ex) {
        OnUpgraded(appliedScripts, ex, currScript);
        var logStr = ex.ToLogString();
        // System.Diagnostics.Debug.WriteLine(logStr);
        _log.LogError(logStr);
        throw;
      } finally {
        if(cmd != null) {
          cmd.Connection = null;
          cmd.Dispose();
        }
        conn.Close();
      }
    }//method

    private void OnHigherVersionDetected() {
      var args = new DataSourceEventArgs(_database, null, DataSourceEventType.HigherVersionDetected);
      _app.DataSourceEvents.OnDataSourceChange(args);
    }


    private void OnUpgrading() {
      var utcNow = _app.TimeService.UtcNow;
      var args = new DbUpgradeEventArgs(_database, DbUpgradeEventType.DbModelUpgrading, _upgradeInfo);
      _app.DataSourceEvents.OnDbUpgrading(args);
    }

    private void OnUpgraded(List<DbUpgradeScript> appliedScripts, Exception exception = null,  DbUpgradeScript failedScript = null) {
      DbUpgradeEventType eventType = exception == null ? DbUpgradeEventType.DbModelUpgraded : DbUpgradeEventType.Error;
      var args = new DbUpgradeEventArgs(_database, eventType, _upgradeInfo, exception, failedScript); 
      _app.DataSourceEvents.OnDbUpgraded(args);
      var logService = (IDbUpgradeLogService) _app.GetService(typeof(IDbUpgradeLogService));
      if(logService != null) {
        var oldVersionInfo = _upgradeInfo.OldDbModel.VersionInfo;
        var oldVersion = oldVersionInfo == null ? DbVersionInfo.ZeroVersion : oldVersionInfo.Version;
        var report = new DbUpgradeReport() { Version = _database.DbModel.VersionInfo.Version, OldDbVersion = oldVersion, 
          MachineName = "(Unknown)",// Environment.MachineName,
          UserName = "(System)", 
          Scripts = appliedScripts, StartedOn = _upgradeInfo.StartedOn, CompletedOn = _upgradeInfo.EndedOn, Exception = exception, FailedScript = failedScript };
        logService.LogDbUpgrade(report);
      }
    }


  }//class
}
