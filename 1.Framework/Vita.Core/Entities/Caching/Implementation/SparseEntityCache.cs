using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Caching {

  
  public class SparseEntityCache {
    CacheSettings _settings;
    ObjectCache<string, CachedRecordData> _cacheTable;  

    public SparseEntityCache(CacheSettings settings) {
      _settings = settings;
      // Uses in fact maxLifetime expiration - because expSec > maxLifetime
      _cacheTable = new ObjectCache<string, CachedRecordData>(
        maxLifeSeconds: _settings.SparseCacheExpirationSec, expirationSeconds: _settings.SparseCacheExpirationSec * 2 ); 
    }

    public EntityRecord Lookup(EntityKey primaryKey, EntitySession session) {
      var strKey = primaryKey.AsString();
      var data = _cacheTable.Lookup(strKey);
      if(data == null)
        return null;
      var needVersion = session.Context.EntityCacheVersion;
      if(data.Version < needVersion) {
        _cacheTable.Remove(primaryKey.AsString());
        return null;
      }
      var rec = new EntityRecord(primaryKey);
      Array.Copy(data.Values, rec.ValuesOriginal, data.Values.Length);
      rec.SourceCacheType = CacheType.Sparse; 
      session.Attach(rec);
      return rec; 
    }

    public void Add(EntityRecord record) {
      var data = new CachedRecordData(record);
      var key = record.PrimaryKey.AsString();
      _cacheTable.Add(key, data);
    }
    public void Remove(EntityRecord record) {
      var key = record.PrimaryKey.AsString();
      _cacheTable.Remove(key); 
    }

    public void Clear() {
      _cacheTable.Clear(); 
    }

    #region nested class CachedRecordData
    class CachedRecordData {
      public long Version; 
      public EntityKey PrimaryKey;
      public object[] Values;
      public CachedRecordData(EntityRecord record) {
        PrimaryKey = record.PrimaryKey;
        Values = new object[record.ValuesOriginal.Length];
        Version = record.Session.Context.EntityCacheVersion;
        Array.Copy(record.ValuesOriginal, Values, Values.Length);
      }
    }
    #endregion
  }
}
