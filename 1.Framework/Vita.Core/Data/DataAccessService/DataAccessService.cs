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
      IDataAccessService
  {
    EntityApp _app;
    IDictionary<string, DataSource> _dataSources;
    object _lock = new object(); 

    public DataAccessService(EntityApp app, EntityCache sharedCache = null) {
      _app = app;
      _events = new DataSourceEvents(this);
      app.RegisterService<IDataAccessService>(this); 
    }

    #region IEntityService methods
    void IEntityService.Init(EntityApp app) {
    }

    void IEntityService.Shutdown() {
      foreach (var ds in GetDataSources())
        ds.Shutdown();
    }
    #endregion

    #region IDataSourceManagement service

    public IEnumerable<DataSource> GetDataSources() {
      return _dataSources.Values; 
    }

    public DataSource GetDataSource(OperationContext context) {
      // We must account for Linked apps, like logging app; the same contet/session might be used for quering entities in main app (book store) and logging app
      // so cached data source in context might not be what we're looking for - it might be from different app. 
      var ds = context.DataSource;
      if(ds != null && ds.Name == context.DataSourceName && ds.App == context.App)
        return ds;
      ds = LookupDataSource(context.DataSourceName);
      if (ds == null) {
        //Fire event 
        var args = new DataSourceAddEventArgs(context);
        Events.OnDataSourceAdd(args);
        ds = args.NewDataSource;
        // if DataSource is returned by event handler, it might be registered or not - it is up to a handler
      }
      Util.Check(ds != null, "Failed to find data source, name: {0}", context.DataSourceName);
      context.DataSource = ds;
      return context.DataSource;
    }

    public void RegisterDataSource(DataSource dataSource) {
      Util.CheckNotEmpty(dataSource.Name, "DataSource name may not be empty.");
      //dataSource.Database.CheckConnectivity();
      lock (_lock) {
        var oldDs = LookupDataSource(dataSource.Name);
        if (oldDs != null)
          return; 
        //create copy, add to it, and then replace with interlock
        IDictionary<string, DataSource> newDict;
        if (_dataSources == null)
          newDict = new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);
        else
          newDict = new Dictionary<string, DataSource>(_dataSources, StringComparer.OrdinalIgnoreCase);
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

    public void RemoveDataSource(string name) {
      lock(_lock) {
        var newDict = new Dictionary<string, DataSource>(_dataSources, StringComparer.OrdinalIgnoreCase);
        newDict.Remove(name);
        _dataSources = newDict; 
      }
    }

    public DataSourceEvents Events {
      get { return _events; }
    } DataSourceEvents _events; 
    #endregion

    public DataConnection GetConnection(EntitySession session, bool admin = false) {
      var ds = GetDataSource(session.Context);
      return ds.Database.GetConnection(session, admin: admin); 
    }

    public DataSource LookupDataSource(string name = DataSource.DefaultName) {
      if(_dataSources == null)
        return null;
      Util.CheckNotEmpty(name, "Data source name may not be null.");
      DataSource ds;
      if(_dataSources.TryGetValue(name, out ds))
        return ds;
      return null;
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


  }
}
