using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Data;
using Vita.Data.Linq;
using Vita.Entities.Locking;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime;

// async methods
public partial class EntitySession {

  public virtual async Task<TEntity> GetEntityAsync<TEntity>(object primaryKeyValue, LoadFlags flags = LoadFlags.Default) where TEntity : class {
    try {
      Util.Check(primaryKeyValue != null, "Session.GetEntity<{0}>(): primary key may not be null.", typeof(TEntity));
      var entityInfo = GetEntityInfo(typeof(TEntity));
      Util.Check(entityInfo.Kind != EntityKind.View, "Cannot use session.GetEntity<TEntity>(PK) method for views. Entity: {0}.", typeof(TEntity));
      //Check if it is an entity key object; if not, it is a "value" (or values) of the key
      var pkType = primaryKeyValue.GetType();
      EntityKey pk = entityInfo.CreatePrimaryKeyInstance(primaryKeyValue);
      var rec = await GetRecordAsync(pk, flags);
      if (rec != null)
        return (TEntity)(object)rec.EntityInstance;
      return default(TEntity);
    } catch (Exception ex) {
      this._appEvents.OnError(this.Context, ex);
      throw;
    }
  }

  public virtual async Task<EntityRecord> GetRecordAsync(EntityKey primaryKey, LoadFlags flags = LoadFlags.Default) {
    if (primaryKey == null || primaryKey.IsNull())
      return null;
    var record = GetLoadedRecord(primaryKey);
    if (record != null) {
      if (record.Status == EntityStatus.Stub && flags.IsSet(LoadFlags.Load))
        record.Reload();
      return record;
    }
    if (flags.IsSet(LoadFlags.Stub))
      return CreateStub(primaryKey);
    if (!flags.IsSet(LoadFlags.Load))
      return null;
    //Otherwise, load it
    var ent = await this.SelectByPrimaryKeyAsync(primaryKey.KeyInfo.Entity, primaryKey.Values);
    return ent?.Record;
  }

  public async Task<IEntityRecordContainer> SelectByPrimaryKeyAsync(EntityInfo entity, object[] keyValues,
          LockType lockType = LockType.None, EntityMemberMask mask = null) {
    var cmd = LinqCommandFactory.CreateSelectByPrimaryKey(this, entity.PrimaryKey, lockType, keyValues);
    var list = (IList) await ExecuteLinqCommandAsync(cmd);
    if (list.Count == 0)
      return null;
    return (IEntityRecordContainer)list[0];
  }

  public virtual async Task<object> ExecuteLinqCommandAsync(LinqCommand command, bool withIncludes = true) {
    try {
      var result = await _dataSource.ExecuteLinqCommandAsync(this, command);
      switch (command.Operation) {
        case LinqOperation.Select:
          Context.App.AppEvents.OnExecutedSelect(this, command);
          if (withIncludes && (command.Includes?.Count > 0 || Context.HasIncludes()))
            await this.RunIncludeQueriesAsync(command, result);
          break;
        default:
          Context.App.AppEvents.OnExecutedNonQuery(this, command);
          break;
      }
      return result;
    } catch (Exception ex) {
      ex.AddValue("entity-command", command + string.Empty);
      ex.AddValue("parameters", command.ParamValues);
      throw;
    }
  }

  private async Task RunIncludeQueriesAsync (LinqCommand command, object mainQueryResult) {
    // initial checks if there's anything to run
    if (mainQueryResult == null)
      return;
    var allIncludes = this.Context.GetMergedIncludes(command.Includes);
    if (allIncludes == null || allIncludes.Count == 0)
      return;
    var resultShape = GetResultShape(mainQueryResult.GetType());
    if (resultShape == QueryResultShape.Object)
      return;
    // Get records from query result
    var records = new List<EntityRecord>();
    switch (resultShape) {
      case QueryResultShape.Entity:
        records.Add(EntityHelper.GetRecord(mainQueryResult));
        break;
      case QueryResultShape.EntityList:
        var list = mainQueryResult as IList;
        if (list.Count == 0)
          return;
        foreach (var ent in list)
          records.Add(EntityHelper.GetRecord(ent));
        break;
    }//switch;
     // actually run the includes
    var entityType = records[0].EntityInfo.EntityType;
    var helper = new IncludeProcessor(this, allIncludes);
    this.LogMessage("------- Running include queries   ----------");
    helper.RunIncludeQueries(entityType, records);
    this.LogMessage("------- Completed include queries ----------");
  }


}
