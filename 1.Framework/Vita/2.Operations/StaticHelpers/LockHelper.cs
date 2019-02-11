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
      /*
      var pkMembers = entInfo.PrimaryKey.KeyMembers;
      Util.Check(pkMembers.Count == 1, "Cannot lock entity {0}: composite primary keys not supported.", entInfo.Name);
      var pkMember = entInfo.PrimaryKey.KeyMembers[0].Member;
      var prmEnt = Expression.Parameter(typeof(TEntity), "e");
      var pkRead = Expression.MakeMemberAccess(prmEnt, pkMember.ClrMemberInfo);

      // PK box - we could use Constant expr to hold PK value directly, but the result is that PK is embedded into translated SQL as literal.
      // (that's how Linq translation works). We want a query with parameter, so that translated linq command is cached and reused. 
      // So we add extra Convert expression to trick LINQ translator. 
      var pkValueExpr = Expression.Convert(Expression.Constant(primaryKey, typeof(object)), pkMember.DataType);
      var eq = Expression.Equal(pkRead, pkValueExpr); // Expression.Constant(primaryKey, pkMember.DataType));
      var filter = Expression.Lambda<Func<TEntity, bool>>(eq, prmEnt);
      */ 
      var entSession = (EntitySession)session;
      EntityKey pk = entInfo.CreatePrimaryKeyInstance(primaryKey);
      var ent = entSession.SelectByPrimaryKey(entInfo, pk.Values, lockType);
      return (TEntity) ent; 
      /*
      var query = session.EntitySet<TEntity>(lockType).Where(filter);
      // We use ToList() on entire query instead of First() because we have already filter on PK value, 
      // and we want to avoid adding any paging (skip/take) clauses to the SQL.
      // We use FirstOrDefult on entire list, and check that we got something; if not, we throw clear message.
      var ent = query.ToList().FirstOrDefault();
      Util.Check(ent != null, "Entity {0} with ID {1} does not exist, cannot set the lock.", entInfo.EntityType.Name,
          primaryKey);
      return ent;
      */
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
