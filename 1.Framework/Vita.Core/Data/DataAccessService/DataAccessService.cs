using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Linq;
using Vita.Entities.Caching;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Services.Implementations;
using Vita.Data.Upgrades;

namespace Vita.Data {

  /// <summary>Manages access to one or more physical databases.  </summary>
  public class DataAccessService :
      IEntityService, //initialization
      IDataAccessService, //data access, inherits IDataStore and IDataSourceManagementService
      IDataSourceManagementService
  {
    EntityApp _app;
    EntityCache _sharedCache;
    IDictionary<string, DataSource> _dataSources;
    object _addLock = new object(); 

    public DataAccessService(EntityApp app, EntityCache sharedCache = null) {
      _app = app;
      _sharedCache = sharedCache;
      app.RegisterService<IDataAccessService>(this);
      app.RegisterService<IDataSourceManagementService>(this); 
    }

    #region IEntityService methods
    void IEntityService.Init(EntityApp app) {
    }

    void IEntityService.Shutdown() {
      if (_sharedCache != null)
        _sharedCache.Shutdown();
      foreach (var ds in GetDataSources())
        ds.Shutdown();
    }
    #endregion

    #region IDataSourceManagement service
    public DataSource GetDataSource(string name = DataSource.DefaultName) {
      if (_dataSources == null)
        return null;
      Util.CheckNotEmpty(name, "Data source name may not be null.");
      DataSource ds;
      if (_dataSources.TryGetValue(name, out ds))
        return ds;
      return null; 
    }

    public IEnumerable<DataSource> GetDataSources() {
      return _dataSources.Values; 
    } 

    public void RegisterDataSource(DataSource dataSource) {
      Util.CheckNotEmpty(dataSource.Name, "DataSource name may not be empty.");
      //dataSource.Database.CheckConnectivity();
      lock (_addLock) {
        var oldDs = GetDataSource(dataSource.Name);
        if (oldDs != null)
          return; 
        //create copy, add to it, and then replace with interlock
        IDictionary<string, DataSource> newDict;
        if (_dataSources == null)
          newDict = new Dictionary<string, DataSource>(StringComparer.InvariantCultureIgnoreCase);
        else
          newDict = new Dictionary<string, DataSource>(_dataSources, StringComparer.InvariantCultureIgnoreCase);
        newDict.Add(dataSource.Name, dataSource);
        System.Threading.Interlocked.Exchange(ref _dataSources, newDict);
      }
      //Invoke upgrade
      _events.OnDataSourceStatusChanging(new DataSourceEventArgs(dataSource, DataSourceEventType.Connecting));
      // Update db model
      var db = dataSource.Database;
      if (db.Settings.UpgradeMode != DbUpgradeMode.Never) {
        var updateMgr = new DbUpgradeManager(dataSource);
        var upgradeInfo = updateMgr.UpgradeDatabase();
        _app.CheckActivationErrors();
        ApplyMigrations(upgradeInfo); 
      }
      _events.OnDataSourceStatusChanging(new DataSourceEventArgs(dataSource, DataSourceEventType.Connected));
    }

    public DataSourceEvents Events {
      get { return _events; }
    } DataSourceEvents _events = new DataSourceEvents();
    #endregion

    public DataConnection GetConnection(EntitySession session, bool admin = false) {
      var ds = LookupDataSource(session.Context);
      return ds.Database.GetConnection(session, admin: admin); 
    }

    private void ApplyMigrations(DbUpgradeInfo upgradeInfo) {
      if (upgradeInfo.PostUpgradeMigrations == null || upgradeInfo.PostUpgradeMigrations.Count == 0)
        return;
      var session = _app.OpenSystemSession();
      foreach (var m in upgradeInfo.PostUpgradeMigrations) {
        m.Action(session);
      }
      session.SaveChanges(); 
    }

    #region IDataAccessService
    public IList<EntityRecord> ExecuteSelect(EntityCommand command, EntitySession session, object[] args) {
      IList<EntityRecord> records; 
      if(this._sharedCache != null) {
        if(_sharedCache != null && _sharedCache.TryExecuteSelect(session, command, args, out records))
          return records;
      }
      var ds = LookupDataSource(session.Context);
      records = ds.ExecuteSelect(session, command, args);
      return records; 

    }

    public void SaveChanges(EntitySession session) {
      var ds = LookupDataSource(session.Context);
      ds.SaveChanges(session); 
    }

    public object ExecuteLinqCommand(LinqCommand command, EntitySession session) {
      //Check shared cache
      object result;
      if(command.CommandType == LinqCommandType.Select && this._sharedCache != null) {
        if(_sharedCache.TryExecuteLinqQuery(session, command, out result))
          return result;
      }
      //Simplified for now
      var ds =  LookupDataSource(session.Context);
      return ds.ExecuteLinqCommand(session, command);
    }
    #endregion

    private DataSource LookupDataSource(OperationContext context) {
      // We must account for Linked apps, like logging app; the same contet/session might be used for quering entities in main app (book store) and logging app
      // so cached data source in context might not be what we're looking for - it might be from different app. 
      if (context.DataSource != null && context.DataSource.Name == context.DataSourceName && context.DataSource.App == context.App)
        return context.DataSource;
      context.DataSource = GetDataSource(context.DataSourceName);
      return context.DataSource;
    }


  }
}
