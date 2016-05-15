using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Caching {

  public class FullyCachedSet {
    public EntityInfo EntityInfo;
    public IDictionary<EntityKey, EntityRecord> RecordsByPrimaryKey;
    public List<EntityRecord> Records;
    public IList Entities;

    public FullyCachedSet(EntityInfo entityInfo, IList<EntityRecord> records) {
      EntityInfo = entityInfo;
      Records = new List<EntityRecord>(); 
      RecordsByPrimaryKey = new Dictionary<EntityKey,EntityRecord>();
      var listType = typeof(List<>).MakeGenericType(EntityInfo.EntityType);
      Entities = Activator.CreateInstance(listType) as IList;
      foreach(var rec in records) {
        RecordsByPrimaryKey[rec.PrimaryKey] = rec;
        Records.Add(rec);
        Entities.Add(rec.EntityInstance);
      }
    }

    public EntityRecord LookupByPrimaryKey(EntityKey primaryKey) {
      EntityRecord rec;
      if (RecordsByPrimaryKey.TryGetValue(primaryKey, out rec))
        return rec;
      return null; 
    }


  } //class

}
