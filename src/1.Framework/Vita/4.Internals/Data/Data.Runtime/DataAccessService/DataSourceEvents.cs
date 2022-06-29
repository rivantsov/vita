using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 
using Vita.Data.Model;
using Vita.Data.Upgrades;
using Vita.Data.Runtime;

namespace Vita.Data.Runtime {

  public class DataSourceEvents {
    EntityApp _app; 

    public event EventHandler<DbUpgradeEventArgs> DbUpgrading;
    public event EventHandler<DbUpgradeEventArgs> DbUpgraded;
    public event EventHandler<DataSourceEventArgs> DataSourceChange;
    public event EventHandler<DataSourceMissingEventArgs> DataSourceMissing;

    internal DataSourceEvents(EntityApp app) {
      _app = app; 
    }

    internal void OnDataSourceChange(DataSourceEventArgs args) {
      DataSourceChange?.Invoke(_app, args);
    }
    internal void OnDbUpgrading(DbUpgradeEventArgs args) {
      DbUpgrading?.Invoke(_app, args);
    }
    internal void OnDbUpgraded(DbUpgradeEventArgs args) {
      DbUpgraded?.Invoke(_app, args);
    }
    internal void OnDataSourceMissing(string name) {
      DataSourceMissing?.Invoke(_app, new DataSourceMissingEventArgs(name));
    }
  }

  public enum DataSourceEventType {
    Connecting,
    HigherVersionDetected,
    Connected,
    Disconnecting,
  }

  public enum DbUpgradeEventType {
    DbModelLoaded,
    DbModelCompared,
    DbModelUpgradeScriptsGenerated,
    DbModelUpgrading,
    DbModelUpgraded,
    MigrationScriptsApplying,
    MigrationScriptsApplied,
    Error,
  }

  public class DataSourceEventArgs : EventArgs {
    public readonly Database Database;
    public readonly DataSourceEventType EventType;
    public string DataSourceName;
    public DataSourceEventArgs(Database database, string dataSourceName, DataSourceEventType eventType) {
      Database = database;
      DataSourceName = dataSourceName;
      EventType = eventType;
    }
  }

  public class DataSourceMissingEventArgs : EventArgs {
    public readonly string DataSourceName;
    public DataSourceMissingEventArgs(string dataSourceName) {
      DataSourceName = dataSourceName;
    }
  }


  public class DbUpgradeEventArgs : EventArgs {
    public readonly Database Database;
    public readonly DbUpgradeEventType EventType;
    public readonly DbUpgradeInfo UpgradeInfo; 
    public readonly DbUpgradeScript FailedScript;
    public readonly Exception Exception;
    public DbUpgradeEventArgs(Database database, DbUpgradeEventType eventType, DbUpgradeInfo upgradeInfo,  Exception exception = null, DbUpgradeScript failedScript = null) {
      Database = database;
      EventType = eventType;
      UpgradeInfo = upgradeInfo; 
      Exception = exception;
      FailedScript = failedScript;
    }
  }



}
