using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Linq;
using Vita.Data;
using Vita.Entities.Logging;

namespace Vita.Entities.Caching {


  public enum CacheLoadStatus {
    NonCurrent, 
    Loading,
    Loaded,
  }

  public class FullSetEntityCache {
    EntityApp _app;
    CacheSettings _settings;
    IDataStore _dataStore;
    StringCaseMode _caseMode; 
    ObjectCache<EntityCacheQuery> _queryCache;
    CacheLoadStatus _loadStatus; 

    public OperationContext OpContext; 
    public DateTime LoadedOn;
    public readonly long CurrentVersion;
    //max known cache version coming from entity sessions;
    long _maxKnownVersion;


    public Dictionary<Type, FullyCachedSet> EntitySets; 

    private static EntityRecord[] _empty = new EntityRecord[] { };
    ITimeService _timeService;
    IErrorLogService _errorLog; 

    public FullSetEntityCache(EntityApp app, CacheSettings settings, IDataStore dataStore, StringCaseMode caseMode) {
      _app = app; 
      _settings = settings;
      _dataStore = dataStore;
      _caseMode = caseMode; 
      _queryCache = new ObjectCache<EntityCacheQuery>("QueryCacheForEntityCache", expirationSecs: 10 * 60);
      _timeService = _app.GetService<ITimeService>();
      _errorLog = _app.GetService<IErrorLogService>(); 
      OpContext = new OperationContext(_app, UserInfo.System);
      CurrentVersion = 0;
      _loadStatus = CacheLoadStatus.NonCurrent;
    }

    private bool CheckFullSetCacheCurrent(EntitySession session) {
      if(EntitySets == null || _loadStatus != CacheLoadStatus.Loaded) {
        if(_loadStatus != CacheLoadStatus.Loading) 
          ReloadAsync("Status-non-current"); 
        return false;
      }
      var currVersion = _maxKnownVersion;
      var sessionVersion = session.Context.EntityCacheVersion;
      bool versionOk = currVersion >= sessionVersion;
      if(versionOk)
        return true; 
      //calc next version
      _maxKnownVersion = Math.Max(currVersion + 1, sessionVersion); //either take version from session, or inc current

      return false;
    }

    //Method is referenced by CacheQueryRewriter, referenced by name
    public IList<EType> GetFullyCachedList<EType>() {
      var entSet = GetFullyCachedSet(typeof(EType));
      Util.Check(entSet != null, "Entity set for type {0} not found in full-set cache.", typeof(EType));
      return (IList<EType>)entSet.Entities;
    }

    public void ReloadAsync(string reason) {
      if(_app.Status != EntityAppStatus.Connected)
        return;
      if(!_app.CacheSettings.CacheEnabled)
        return;
      _loadStatus = CacheLoadStatus.Loading;
      var task = new System.Threading.Tasks.Task(Reload, reason);
      task.Start();
    }


   public void Invalidate(bool reload = false, bool waitForReloadComplete = false) {
     _loadStatus = CacheLoadStatus.NonCurrent;
     if(OpContext.LocalLog != null)
       OpContext.LocalLog.Info("---- Full-set cache invalidated.");
     // _maxKnownVersion++;
     if(reload)
       ReloadAsync("invalidate");
     if(waitForReloadComplete) {
       var endBefore = _timeService.UtcNow.AddSeconds(5); 
       while(_loadStatus != CacheLoadStatus.Loaded && _timeService.UtcNow < endBefore)
         Thread.Yield();
     }
    }

    public void Reload(object reason) {
      _loadStatus = CacheLoadStatus.Loading;
      if(_app.Status != EntityAppStatus.Connected) //stop if we're in shutdown
        return;
      var session = (EntitySession)this.OpContext.OpenSystemSession();
      try {
        session.LogMessage("------- Full-set cache - reloading/" + reason);
        var entSets = new Dictionary<Type, FullyCachedSet>();
        foreach(var entType in _settings.FullSetCacheTypes) {
          if(_loadStatus != CacheLoadStatus.Loading) {
            session.LogMessage("------- Full-set cache - reload canceled.");
            return; // stop if another cache invalidation request came in
          }
          var entInfo = _app.Model.GetEntityInfo(entType);
          var cmd = entInfo.CrudCommands.SelectAll;
          var records = _dataStore.ExecuteSelect(cmd, session, null);
          var entSet = new FullyCachedSet(entInfo, records);
          entSets.Add(entType, entSet);
        }
        this.EntitySets = entSets;
        LoadedOn = _timeService.UtcNow;
        _loadStatus = CacheLoadStatus.Loaded;
        session.LogMessage("------- Full-set cache - reloaded.");
      } catch(Exception ex) {
        _loadStatus = CacheLoadStatus.NonCurrent;
        _errorLog.LogError(ex, this.OpContext);
      } finally {
        
      }
    }

    public bool Expired() {
      var expired = _loadStatus == CacheLoadStatus.Loaded && 
          LoadedOn.AddSeconds(_settings.FullSetCacheExpirationSec) < _timeService.UtcNow;
      if(expired && _loadStatus == CacheLoadStatus.Loaded)
        _loadStatus = CacheLoadStatus.NonCurrent;
      return expired;
    }

    #region Querying
    public bool TryExecuteSelect(EntitySession session, EntityCommand command, object[] args, out IList<EntityRecord> records) {
      records = null; 
      if(!CheckFullSetCacheCurrent(session))
        return false; 
      var result = TryExecuteSelectImpl(session, command, args, out records);
      if(result && records != null && records.Count > 0) {
        session.Context.EntityCacheVersion = CurrentVersion;
        records = EntityCacheHelper.CloneAndAttach(session, records);
      }
      return result;
    }

    private bool TryExecuteSelectImpl(EntitySession session, EntityCommand command, object[] args, out IList<EntityRecord> records) {
      records = _empty;
      var cacheType = command.TargetEntityInfo.CacheType;
      var entType = command.TargetEntityInfo.EntityType;
      var fullSet = GetFullyCachedSet(entType);
      if(fullSet == null)
        return false;
      switch(command.Kind) {
        case EntityCommandKind.SelectAll:
        case EntityCommandKind.SelectAllPaged:
          if(cacheType != CacheType.FullSet)
            return false;
          if(command.Kind == EntityCommandKind.SelectAllPaged && args.Length == 2)
            records = fullSet.Records.Skip((int)args[0]).Take((int)args[1]).ToList();
          else
            records = fullSet.Records;
          return true;

        case EntityCommandKind.SelectByKeyArray:
          if(cacheType != CacheType.FullSet)
            return false;
          //create hashset of values
          HashSet<object> idSet = new HashSet<object>();
          var iEnum = args[0] as IEnumerable;
          foreach (var v in iEnum)
            idSet.Add(v);
          records = fullSet.Records.Where(r => idSet.Contains(r.PrimaryKey.Values[0])).ToList();
          return true; 

        case EntityCommandKind.SelectByKey:
          if(command.SelectKey.KeyType.IsSet(KeyType.PrimaryKey)) {
            var pk = new EntityKey(command.SelectKey, args);
            var rec = fullSet.LookupByPrimaryKey(pk);
            if(rec != null) {
              records = new EntityRecord[] { rec };
              return true;
            } else {
              records = _empty;
              return false; // not found, records already set to empty array
            }
          }//if PrimaryKey 
          if(command.SelectKey.KeyType.IsSet(KeyType.ForeignKey)) {
            records = fullSet.Records.Where(rec => rec.KeyMatches(command.SelectKey, args)).ToList();
            return true;
          }
          return false;

        default:
          return false; //never happens
      }//switch
    }

    public bool TryExecuteDynamicQuery(EntitySession session, LinqCommand command, out object result) {
      result = null;
      if(!CheckFullSetCacheCurrent(session))
        return false;
      var cmdInfo = command.Info; 
      //try getting previously compiled version or compile it
      var cacheQuery = GetCacheQuery(command);
      if (cacheQuery == null)
        return false;
      var start = _timeService.ElapsedMilliseconds;
      result = cacheQuery.CacheFunc(session, this, command.ParameterValues);
      var end = _timeService.ElapsedMilliseconds;
      var logEntry = new CacheQueryLogEntry(session.Context, cacheQuery.LogString, command.ParameterValues,
                                _timeService.UtcNow, end - start, GetRowCount(result));
      session.AddLogEntry(logEntry);
      session.Context.EntityCacheVersion = CurrentVersion;
      return true;
    }

    private int GetRowCount(object queryResult) {
      if(queryResult == null)
        return 0;
      var rList = queryResult as IList;
      return rList == null ? 1 : rList.Count;
    }

    public FullyCachedSet GetFullyCachedSet(Type entityType) {
      if(EntitySets == null)
        return null; 
      FullyCachedSet entSet;
      if(EntitySets.TryGetValue(entityType, out entSet))
        return entSet;
      return null;
    }

    private EntityCacheQuery GetCacheQuery(LinqCommand command) {
      // Try to lookup in query cache
      var cacheQuery = _queryCache.Lookup(command.Info.CacheKey);
      if(cacheQuery != null) 
        return cacheQuery;
      // Build query and save it in entity cache
      //Preprocess query to get all entity types used 
      LinqCommandPreprocessor.PreprocessCommand(_app.Model, command);
      foreach(var ent in command.Info.Entities)
        if(ent.CacheType != CacheType.FullSet)
          return null; //cannot use cache for this query
      //Rewrite query for cache
      // string case mode is either case insensitive when query option is set (search queries are case insensitive)
      //  or set to default value for cache (which is the same as DB-s default case value)
      var queryCaseMode = command.Info.Options.IsSet(QueryOptions.ForceIgnoreCase) ? StringCaseMode.CaseInsensitive : _caseMode;  
      var rewriter = new CacheQueryRewriter(_app.Model, queryCaseMode);
      var cacheFunc = rewriter.Rewrite(command.Info.Lambda);
      cacheQuery = new EntityCacheQuery(cacheFunc, command.Info.Lambda.ToString());
      _queryCache.Add(command.Info.CacheKey, cacheQuery);
      return cacheQuery;
    }
    #endregion

  }
}
