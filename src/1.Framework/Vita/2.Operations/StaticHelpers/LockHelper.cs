using System;
using System.Collections.Generic;
using System.Linq.Expressions; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data;
using System.Reflection;

namespace Vita.Entities.Locking {
  
  public enum LockType {
    None = 0, 
    NoLock, 
    SharedRead, 
    ForUpdate,
  }


  public static class LockHelper {

    public static bool HasLock(this LockType lockType) {
      return lockType == LockType.SharedRead || lockType == LockType.ForUpdate;
    }

    public static IQueryable<TEntity> EntitySet<TEntity>(this IEntitySession session, LockType lockType) where TEntity: class {
      if (lockType == LockType.None)
        return session.EntitySet<TEntity>(); 
      var entSession = (EntitySession)session;
      return entSession.CreateEntitySet<TEntity>(lockType);
    }

    /// <summary>Retrieves entity by type and primary key value and sets database lock on the underlying record. </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="primaryKey">The value of the primary key.</param>
    /// <param name="lockType">Lock type.</param>
    /// <returns>An entity instance.</returns>
    /// <remarks>For composite primary keys pass an instance of primary key
    /// created using the <see cref="EntitySessionExtensions.CreatePrimaryKey"/> extension method. 
    /// </remarks>
    public static TEntity GetEntity<TEntity>(this IEntitySession session, object primaryKey, LockType lockType)
                     where TEntity: class {
      if (lockType == LockType.None)
        return session.GetEntity<TEntity>(primaryKey); //short path, no locks
      Util.CheckParam(primaryKey, nameof(primaryKey));
      session.LogMessage("-- Locking entity {0}/{1}", typeof(TEntity).Name, primaryKey);
      var entInfo = session.Context.App.Model.GetEntityInfo(typeof(TEntity), throwIfNotFound: true);
      var entSession = (EntitySession)session;
      EntityKey pk = entInfo.CreatePrimaryKeyInstance(primaryKey);
      var ent = entSession.SelectByPrimaryKey(entInfo, pk.Values, lockType);
      return (TEntity) ent; 
    }

    public static void ReleaseLocks(this IEntitySession session) {
      var entSession = (EntitySession)session;
      var currConn = entSession.CurrentConnection;
      if(currConn == null || currConn.DbTransaction == null)
        return;
      session.LogMessage("-- Releasing locks ");
      currConn.Abort();
      if(currConn.Lifetime != DbConnectionLifetime.Explicit) {
        currConn.Close();
        entSession.CurrentConnection = null;
      }
    }

  } //class

}
