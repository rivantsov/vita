using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using Vita.Common;
using Vita.Entities.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Data;
using Vita.Entities.Logging;

namespace Vita.Entities.Caching {

  public class EntityCache {
    public CacheSettings Settings;
    EntityApp _app;
    IDataStore _dataStore;
    ITimeService _timeService; 
    IOperationLogService _logService;
    IErrorLogService _errorLog;

    FullSetEntityCache _fullSetCache;
    SparseEntityCache _sparseCache;

    public EntityCache(EntityApp app, CacheSettings settings, Database database) {
      _app = app;
      Settings = settings;
      _dataStore = database;
      _sparseCache = new SparseEntityCache(Settings);
      var dbIsCaseInsensitive = database.Settings.Driver.Features.IsSet(Data.Driver.DbFeatures.DefaultCaseInsensitive);
      var caseMode = dbIsCaseInsensitive ? StringCaseMode.CaseInsensitive : StringCaseMode.CaseSensitive;
      _fullSetCache = new FullSetEntityCache(_app, Settings, _dataStore, caseMode);
      _timeService = _app.GetService<ITimeService>();
      _logService = _app.GetService<IOperationLogService>();
      _errorLog = _app.GetService<IErrorLogService>();
      MarkCachedEntities();
      _app.AppEvents.SavedChanges += Events_SavedChanges;
    }

    public void Shutdown() {
    }

    public void Invalidate(bool reload = false, bool waitForReloadComplete = false) {
      _sparseCache.Clear();
      _fullSetCache.Invalidate(reload, waitForReloadComplete);
    }

    private void MarkCachedEntities() {
      //Mark entities as cached
      foreach(var entType in Settings.SparseCacheTypes)
        SetupEntity(entType, CacheType.Sparse);
      foreach(var entType in Settings.FullSetCacheTypes)
        SetupEntity(entType, CacheType.FullSet);
    }
    private void SetupEntity(Type entityType, CacheType cacheType) {
      var model = _app.Model;
      var entInfo = model.GetEntityInfo(entityType);
      Util.Check(entInfo != null, "Type {0} specified in cache settings is not an entity.", entityType);
      entInfo.Flags |= EntityFlags.Cached;
      entInfo.CacheType = cacheType;
    }

    private static EntityRecord[] _empty = new EntityRecord[] { };

    public bool TryExecuteSelect(EntitySession session, EntityCommand command, object[] args, out IList<EntityRecord> records) {
      records = _empty;
      if(!Settings.CacheEnabled || session.CacheDisabled)
        return false; 
      if (!string.IsNullOrWhiteSpace(command.Filter))
        return false; 
      var cachingType = command.TargetEntityInfo.CacheType;
      if(cachingType == CacheType.None)
        return false;
      switch(cachingType) {
        case CacheType.None: return false; 
        case CacheType.FullSet:
          var start = _timeService.ElapsedMilliseconds; 
          var result = _fullSetCache.TryExecuteSelect(session, command, args, out records);
          if(result) {
            var end = _timeService.ElapsedMilliseconds; 
            var rowCount = records == null ? 0 : records.Count;
            LogCommand(session, command, args, cachingType, end - start, rowCount);
          }
          return result; 

        case CacheType.Sparse:
          var getByPk = command.Kind == EntityCommandKind.SelectByKey && command.SelectKey.KeyType.IsSet(KeyType.PrimaryKey);
          if(getByPk) {
            var pk = new EntityKey(command.SelectKey, args);
            var rec = _sparseCache.Lookup(pk, session);
            if(rec != null) {
              records = new EntityRecord[] { rec };
              LogCommand(session, command, args, cachingType, 0, 1);
              return true; 
            }
          }
          return false; 
      }//switch
      return false; 
    }

    public bool TryExecuteLinqQuery(EntitySession session, LinqCommand command, out object result) {
      result = null;
      if(!Settings.CacheEnabled || session.CacheDisabled || command.Info.Options.IsSet(QueryOptions.NoEntityCache))
        return false;
      if(_fullSetCache.TryExecuteDynamicQuery(session, command, out result)) {
        return true;
      }
      return false;
    }

    private void LogCommand(EntitySession session, EntityCommand command, object[] args, CacheType cachingType, long time, int rowCount) {
      var logEntry = new CacheCommandLogEntry(session.Context, command.CommandName, args, _timeService.UtcNow, time, rowCount, cachingType);
      session.AddLogEntry(logEntry);
    }

    //  Adding to cache - we add only sparse cached records from queries, and ignore fully-cached entities
    public void CacheRecords(IList<EntityRecord> records) {
      if(!Settings.CacheEnabled)
        return; 
      if(records == null || records.Count == 0)
        return;
      var session = records[0].Session;
      if(session.CacheDisabled)
        return; 
      var ent = records[0].EntityInfo;
      if (ent.CacheType != CacheType.Sparse)
        return; 
      foreach(var rec in records)
        _sparseCache.Add(rec);
    }



    void Events_SavedChanges(object sender, EntitySessionEventArgs args) {
      var session = (EntitySession)args.Session; 
      bool fullSetCacheUpdated = false; 
      // important - use for-i loop here
      for(int i=0; i < session.RecordsChanged.Count; i++) {
        var rec = session.RecordsChanged[i];
        switch(rec.EntityInfo.CacheType) {
          case CacheType.FullSet:
            fullSetCacheUpdated = true; 
            break; 
          case CacheType.Sparse:
            switch(rec.StatusBeforeSave) {
              case EntityStatus.Deleting: 
                _sparseCache.Remove(rec); break; 
              default:
                _sparseCache.Add(rec);
                break; 
            }//switch
            break; 
        }
      }
      if(fullSetCacheUpdated) {
        _fullSetCache.Invalidate(); //it will be reloaded
        //_maxKnownVersion++; //tick up the version
      }
    }//method

    private bool HasCacheUpdates(EntitySession session) {
      return session.RecordsChanged.Any(r => r.EntityInfo.CacheType != CacheType.None);
    }

  
  
  
  }//class
}
