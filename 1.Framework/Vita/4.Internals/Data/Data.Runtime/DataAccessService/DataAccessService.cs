using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services.Implementations;
using Vita.Data.Model;

namespace Vita.Data.Runtime {

  /// <summary>Manages access to one or more physical databases.  </summary>
  public class DataAccessService :
      IEntityServiceBase, //initialization
      IDataAccessService
  {
    EntityApp _app;
    IDictionary<string, DataSource> _dataSources = 
        new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);
    object _lock = new object(); 

    public DataAccessService(EntityApp app) {
      _app = app;
    }

    #region IEntityService methods
    void IEntityServiceBase.Init(EntityApp app) {
    }

    void IEntityServiceBase.Shutdown() {
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
      // NOTE: this might not be true anymore; decide later to allow or not to use entities from linked apps
      var ds = context.LastDataSource;
      if(ds != null && ds.Name == context.DataSourceName && ds.App == context.App)
        return ds;
      ds = LookupDataSource(context.DataSourceName);
      if (ds == null) {
        //Fire event, let custom handler add data source and lookup again
        _app.DataSourceEvents.OnDataSourceMissing(context.DataSourceName);
        ds = LookupDataSource(context.DataSourceName);
      }
      Util.Check(ds != null, "Failed to find data source, name: {0}", context.DataSourceName);
      context.LastDataSource = ds;
      return ds;
    }

    public void RegisterDataSource(DataSource dataSource) {
      lock (_lock) {
        var oldDs = LookupDataSource(dataSource.Name);
        if (oldDs != null)
          return; 
        //create copy, add to it, and then replace with interlock
        IDictionary<string, DataSource> newDict;
        if (_dataSources == null || _dataSources.Count == 0)
          newDict = new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);
        else
          newDict = new Dictionary<string, DataSource>(_dataSources, StringComparer.OrdinalIgnoreCase);
        newDict.Add(dataSource.Name, dataSource);
        System.Threading.Interlocked.Exchange(ref _dataSources, newDict);
      }
      _app.DataSourceEvents.OnDataSourceChange(new DataSourceEventArgs(dataSource.Database, dataSource.Name, DataSourceEventType.Connected));
    }

    public void RemoveDataSource(string name) {
      lock(_lock) {
        var ds = LookupDataSource(name);
        Util.Check(ds != null, "Datasource {0} not found.", name);
        _app.DataSourceEvents.OnDataSourceChange(new DataSourceEventArgs(ds.Database, ds.Name, DataSourceEventType.Disconnecting));
        var newDict = new Dictionary<string, DataSource>(_dataSources, StringComparer.OrdinalIgnoreCase);
        newDict.Remove(name);
        System.Threading.Interlocked.Exchange(ref _dataSources, newDict);
      }
    }
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



  }
}
