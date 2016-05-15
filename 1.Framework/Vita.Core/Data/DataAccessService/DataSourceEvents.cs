using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Upgrades;

namespace Vita.Data {
  public class DataSourceEvents {

    public event EventHandler<DbUpgradeEventArgs> DbUpgrading;
    public event EventHandler<DbUpgradeEventArgs> DbUpgraded;
    public event EventHandler<DataSourceEventArgs> DataSourceStatusChanging;

    internal void OnDataSourceStatusChanging(DataSourceEventArgs args) {
      if(DataSourceStatusChanging != null)
        DataSourceStatusChanging(this, args);
    }
    internal void OnDbUpgrading(DbUpgradeEventArgs args) {
      if(DbUpgrading != null)
        DbUpgrading(this, args);
    }
    internal void OnDbUpgraded(DbUpgradeEventArgs args) {
      if(DbUpgraded != null)
        DbUpgraded(this, args);
    }
  }

  public enum DataSourceEventType {
    Connecting,
    HigherVersionDetected,
    DbModelUpdating,
    DbModelUpdated,
    Connected,
  }

  public class DataSourceEventArgs : EventArgs {
    public readonly DataSource DataSource;
    public DataSourceEventType EventType;
    public DataSourceEventArgs(DataSource dataSource, DataSourceEventType eventType) {
      DataSource = dataSource;
      EventType = eventType;
    }
    public DbVersionInfo DbVersionInfo { get { return DataSource.Database.DbModel.VersionInfo; } }
    public Version AppVersion { get { return DataSource.App.Version; } }
  }

  public class DbUpgradeEventArgs : EventArgs {
    public readonly DataSource DataSource;
    public readonly List<DbUpgradeScript> Scripts;
    public readonly DateTime StartedOn;
    public readonly DateTime? CompletedOn;
    public readonly DbUpgradeScript FailedScript;
    public readonly Exception Exception;
    public DbUpgradeEventArgs(DataSource dataSource, DataSourceEventType eventType, List<DbUpgradeScript> scripts, 
                                  DateTime startedOn, DateTime? completedOn,
                                  Exception exception = null, DbUpgradeScript failedScript = null) {
      DataSource = dataSource;
      Scripts = scripts;
      StartedOn = startedOn;
      CompletedOn = completedOn;
      Exception = exception;
      FailedScript = failedScript;
    }
  }



}
