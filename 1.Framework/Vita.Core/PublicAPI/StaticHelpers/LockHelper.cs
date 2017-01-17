using System;
using System.Collections.Generic;
using System.Linq.Expressions; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Linq;
using Vita.Entities.Runtime;
using Vita.Common;
using Vita.Data;

namespace Vita.Entities.Locking {
  
  [Flags]
  public enum LockOptions {
    None = 0, 
    NoLock = 1, 
    SharedRead = 1 << 1, 
    ForUpdate = 1 << 2,
  }

  public interface ILockTarget {
    LockOptions LockOptions { get; }
  }


  public static class LockHelper {

    public static bool IsSet(this LockOptions options, LockOptions option) {
      return (options & option) != 0;
    }
    public static IQueryable<TEntity> EntitySet<TEntity>(this IEntitySession session, LockOptions options) where TEntity: class {
      if (options == LockOptions.None)
        return session.EntitySet<TEntity>(); 
      var entSession = (EntitySession)session;
      return entSession.CreateEntitySet<TEntity>(options);
    }

    //For now implemented using dynamically built LINQ query; stored proc support to come later
    // TODO: save filter Func in EntityInfo and reuse it
    public static TEntity GetEntity<TEntity>(this IEntitySession session, object primaryKey, LockOptions options)
                     where TEntity: class {
      if (options == LockOptions.None)
        return session.GetEntity<TEntity>(primaryKey); //short path, no locks
      session.LogMessage("-- Locking entity {0}/{1}", typeof(TEntity).Name, primaryKey);
      var entInfo = session.Context.App.Model.GetEntityInfo(typeof(TEntity), throwIfNotFound: true);
      var pkMembers = entInfo.PrimaryKey.KeyMembers;
      Util.Check(pkMembers.Count == 1, "Cannot lock entity {0}: composite primary keys not supported.", entInfo.Name);
      var pkMember = entInfo.PrimaryKey.KeyMembers[0].Member;
      var prmEnt = Expression.Parameter(typeof(TEntity), "e");
      var pkRead = Expression.MakeMemberAccess(prmEnt, pkMember.ClrMemberInfo);
      var eq = Expression.Equal(pkRead, Expression.Constant(primaryKey, pkMember.DataType));
      var filter = Expression.Lambda<Func<TEntity, bool>>(eq, prmEnt);
      var query = session.EntitySet<TEntity>(options).Where(filter);
      // We use ToList() on entire query instead of First() because we have already filter on PK value, 
      // and we want to avoid adding any paging (skip/take) clauses to the SQL.
      // We use FirstOrDefult on entire list, and check that we got something; if not, we throw clear message.
      var ent = query.ToList().FirstOrDefault();
      Util.Check(ent != null, "Entity {0} with ID {1} does not exist, cannot set the lock.", entInfo.EntityType.Name,
          primaryKey);
      return ent;
      /*
      //The following is just a sketch
      if (checkLastModifiedId != null) {
        Util.Check(entInfo.VersioningMember != null, "Entity {0} has no tracking/versioning column (last modified transaction id), cannot check data version.", entInfo.Name);
        var lastTransId = EntityHelper.GetProperty<Guid>(ent, entInfo.VersioningMember.MemberName);
        session.Context.ThrowIf(doNotUse_checkLastModifiedId.Value != lastTransId, ClientFaultCodes.ConcurrentUpdate, entInfo.VersioningMember.MemberName, "Entity {0} was modified by concurrent process.", entInfo.Name); 
      }
       * */
    }

    public static void ReleaseLocks(this IEntitySession session) {
      var entSession = (EntitySession)session;
      var currConn = entSession.CurrentConnection; 
      if (currConn == null || currConn.DbTransaction == null)
        return;
      session.LogMessage("-- Releasing locks ");
      currConn.Abort();
      if (currConn.Lifetime != ConnectionLifetime.Explicit) {
        currConn.Close();
        entSession.CurrentConnection = null; 
      }
    }    

  } //class

}
