using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Linq;
using Vita.Data.Runtime;
using Vita.Data.Model;

namespace Vita.Data.Runtime {

  /// <summary>DataSource is a tuple combining a Database and associated data cache.</summary>
  /// <remarks>All select operations are first checked with cache, then submitted to the database.</remarks>
  public class DataSource  {
    public const string DefaultName = "(Default)";

    public readonly string Name; 
    public readonly EntityApp App;
    public readonly IEntityCache  Cache;
    public readonly Database Database;

    public DataSource(string name, Database database) {
      Database = database;
      App = Database.DbModel.EntityApp;
      Name = database.Settings.DataSourceName; 
    }

    public void Shutdown() {
      if(Cache != null)
        Cache.Shutdown();
      Database.Shutdown();
    }


    public void SaveChanges(EntitySession session) {
      Database.SaveChanges(session);
      if(Cache != null)
        Cache.OnSavedChanges(session);  
    }

    public async Task SaveChangesAsync(EntitySession session) {
      await Database.SaveChangesAsync(session);
      if (Cache != null)
        Cache.OnSavedChanges(session);
    }

    public object ExecuteLinqCommand(EntitySession session, LinqCommand command) {
      object result = null;
      if(Cache != null && command.Operation == LinqOperation.Select && Cache.TryExecuteSelect(session, command, out result))
        return result;
      result = Database.ExecuteLinqCommand(session, command);
      //If we are returning entities, cache them; if updating - invalidate
      if(Cache != null)
        Cache.OnCommandExecuted(session, command, result);
      return result; 
    }

    public async Task<object> ExecuteLinqCommandAsync(EntitySession session, LinqCommand command) {
      object result = null;
      if (Cache != null && command.Operation == LinqOperation.Select && Cache.TryExecuteSelect(session, command, out result))
        return result;
      result = await Database.ExecuteLinqCommandAsync(session, command);
      //If we are returning entities, cache them; if updating - invalidate
      if (Cache != null)
        Cache.OnCommandExecuted(session, command, result);
      return result;
    }

    public DataConnection GetConnection(EntitySession session, bool admin = false) {
      return this.Database.GetConnection(session, admin: admin); 
    }

  }
}
