using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Model;
using Vita.Entities.Caching;

namespace Vita.Data {

  /// <summary>DataSource is a tuple combining a Database and associated data cache.</summary>
  /// <remarks>All select operations are first checked with cache, then submitted to the database.</remarks>
  public class DataSource {
    public const string DefaultName = "(Default)";

    public readonly string Name; 
    public readonly EntityApp App;
    public readonly EntityCache Cache;
    public readonly Database Database;

    public DataSource(string name, Database database, CacheSettings cacheSettings = null) {
      Database = database;
      App = Database.DbModel.EntityApp;
      Name = database.Settings.DataSourceName; 
      if(cacheSettings != null && cacheSettings.HasTypes())
        Cache = new EntityCache(App, cacheSettings, this.Database); 
    }

    public void Shutdown() {
      if(Cache != null)
        Cache.Shutdown();
      Database.Shutdown();
    }

    public IList<EntityRecord> ExecuteSelect(EntitySession session, EntityCommand command, object[] args) {
      IList<EntityRecord> records;
      if(Cache != null && Cache.TryExecuteSelect(session, command, args, out records))
          return records;
      records = Database.ExecuteSelect(command, session, args);
      if(Cache != null)
        Cache.CacheRecords(records);
      return records; 
    }

    public void SaveChanges(EntitySession session) {
      Database.SaveChanges(session);
    }

    public object ExecuteLinqCommand(EntitySession session, LinqCommand command) {
      object result;
      if(command.CommandType == LinqCommandType.Select && Cache != null && Cache.TryExecuteLinqQuery(session, command, out result))
        return result;
      result = Database.ExecuteLinqCommand(command, session);
      //If we are returning entities, cache them
      if(command.CommandType == LinqCommandType.Select) {
        var recs = result as IList<EntityRecord>;
        if(Cache != null && recs != null)
          Cache.CacheRecords(recs); //adds to sparse cache
      } else {
        // Update/Insert/Delete statemetns
        if (Cache != null && command.TargetEntity.CacheType != CacheType.None)
          Cache.Invalidate();
      }
      return result; 
    }

  }
}
