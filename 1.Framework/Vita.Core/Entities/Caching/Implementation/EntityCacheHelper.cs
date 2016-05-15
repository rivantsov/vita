using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Caching {

  public static class EntityCacheHelper {
    public static MethodInfo CloneEntityMethod;
    public static MethodInfo CloneEntitiesMethod;
    public static MethodInfo EntitiesEqualMethod;
    public static MethodInfo EntitiesNotEqualMethod;
    public static MethodInfo StringStaticEquals3Method;
    public static MethodInfo StringStartsWith1Method; //string.StartsWith(value) method
    public static MethodInfo StringStartsWith2Method; //string.StartsWith(value, comparisonType)
    public static MethodInfo GetEntityListMethod;
    public static ConstantExpression ConstInvariantCulture;
    public static ConstantExpression ConstInvariantCultureIgnoreCase;


    static EntityCacheHelper() {
      var thisType = typeof(EntityCacheHelper); 
      var flags = BindingFlags.Static | BindingFlags.Public;
      CloneEntityMethod = thisType.GetMethod("CloneEntity",  flags);
      CloneEntitiesMethod = thisType.GetMethod("CloneEntities", flags);
      EntitiesEqualMethod = thisType.GetMethod("EntitiesEqual", flags);
      EntitiesNotEqualMethod = thisType.GetMethod("EntitiesNotEqual", flags);
      StringStaticEquals3Method = typeof(string).GetMethods(flags).First(m => m.Name == "Equals" && m.GetParameters().Length == 3);
      GetEntityListMethod = typeof(FullSetEntityCache).GetMethod("GetFullyCachedList");
      flags = BindingFlags.Public | BindingFlags.Instance; 
      StringStartsWith1Method = typeof(string).GetMethods(flags).First(m => m.Name == "StartsWith" && m.GetParameters().Length == 1);
      StringStartsWith2Method = typeof(string).GetMethods(flags).First(m => m.Name == "StartsWith" && m.GetParameters().Length == 2);
      ConstInvariantCulture = Expression.Constant(StringComparison.InvariantCulture);
      ConstInvariantCultureIgnoreCase = Expression.Constant(StringComparison.InvariantCultureIgnoreCase);
    }


    //Static methods used in rewritten queries againts cache. Calls to these methods is added to query expressions
    // to automatically clone entities coming from cache.
    // Note: doing iterator here (with yield return) does not work here - errors out for queries returning lists of lists
    public static IList<TEntity> CloneEntities<TEntity>(EntitySession session, IEnumerable<TEntity> entities) {
      var result = new List<TEntity>();
      foreach (TEntity entity in entities) {
        var ent = CloneEntity(session, entity);
        if (ent != null)
          result.Add(ent);
      }
      return result;
    }

    public static TEntity CloneEntity<TEntity>(EntitySession session, TEntity entity) {
      if (entity == null)
        return default(TEntity);
      var rec = EntityHelper.GetRecord(entity);
      var clone = CloneAndAttach(session, rec);
      return (TEntity)(object)clone.EntityInstance;
    }

    #region Cloning and attaching records
    // forceFullCopy is true when we put (update) records into CacheSession
    public static IList<EntityRecord> CloneAndAttach(EntitySession toSession, IList<EntityRecord> records, bool forceFullCopy = false) {
      var newRecs = new List<EntityRecord>();
      for(int i = 0; i < records.Count; i++)
        newRecs.Add(CloneAndAttach(toSession, records[i], forceFullCopy));
      return newRecs;
    }

    public static EntityRecord CloneAndAttach(EntitySession toSession, EntityRecord record, bool forceFullCopy = false) {
      if(record.Session == toSession)
        return record; //do not clone if it is already in this session
      //If record with the same PK is already loaded in session, use it in result; otherwise, clone record from cache and attach
      var sessionRec = toSession.GetRecord(record.PrimaryKey, LoadFlags.None);
      if(sessionRec == null) {
        sessionRec = new EntityRecord(record); //make clone
        toSession.Attach(sessionRec);
      } else if(sessionRec.Status == EntityStatus.Stub || forceFullCopy) {
        sessionRec.CopyOriginalValues(record);
      }
      sessionRec.SourceCacheType = CacheType.FullSet;
      return sessionRec;
    }
    #endregion


    // Used in comparison operations in dynamic LINQ queries executed in cache. Accounts for the fact that entities might be attached to different sessions.
    // The situation comes up when we use an entity instance as parameter in dynamic query: 'var msBooks = session.EntitySet<IBook>().Where(b => b.Publisher == msPub);'
    // In this query 'msPub' is a local variable, an entity attached to local session. During execution it is matched against entities attached to cache session,
    // so by default '==' operator would not work, as MS publisher entity is a different object in both session. To make it work, the equal and not-equal operators
    // in LINQ queries are replaced with a call to EntitiesEqual, which compares primary keys if entities belong to different sessions.
    public static bool EntitiesEqual(object x, object y) {
      if (x == y) return true;
      if (x == null) return y == null;
      if (y == null) return x == null; 
      // both are not nulls, and not the same object; there's a chance they are identical entities from different sessions
      var rx = EntityHelper.GetRecord(x);
      var ry = EntityHelper.GetRecord(y);
      if (rx.Session == ry.Session)
        return rx == ry; //compare as objects
      // Sessions are different - compare entity types and primary keys
      if (rx.EntityInfo != ry.EntityInfo) //should never happen, but just in case 
        return false; 
      var keysEqual = rx.PrimaryKey.Equals(ry.PrimaryKey);
      return keysEqual; 
    }

    public static bool EntitiesNotEqual(object x, object y) {
      return !EntitiesEqual(x, y);
    }

  }
}
