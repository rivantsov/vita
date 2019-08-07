using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities {

  public static partial class EntitySessionExtensions {


    /// <summary>Returns a list of entities.</summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="skip">Optional. A number of entities to skip.</param>
    /// <param name="take">Maximum number of entities to include in results.</param>
    /// <param name="orderBy">Order by expression.</param>
    /// <param name="descending">Descening order flag.</param>
    /// <returns>A list of entities.</returns>
    public static IList<TEntity> GetEntities<TEntity>(this IEntitySession session,
          Expression<Func<TEntity, object>> orderBy = null, bool descending = false,
          int? skip = null, int? take = null) where TEntity : class {
      var query = session.EntitySet<TEntity>();
      if (orderBy != null)
        query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
      if(skip != null)
        query = query.Skip(skip.Value);
      if(take != null)
        query = query.Take(take.Value);
      return query.ToList();
    }

    public static void LogMessage(this IEntitySession session, string message, params object[] args) {
      var entitySession = (EntitySession)session;
      entitySession.AddLogEntry(new InfoLogEntry(session.Context.LogContext, message, args));
    }

    public static System.Data.IDbCommand GetLastCommand(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.LastCommand;
    }

    public static long GetNextTransactionId(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.GetNextTransactionId();
    }

    public static IDisposable WithElevatedRead(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.ElevateRead(); 
    }

    /// <summary>Enables/disables local log temporarily.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="enable">Optional, boolean flag indicating whether log should enabled or disabled.</param>
    public static void EnableLog(this IEntitySession session, bool enable = true) {
      var ext = (EntitySession)session;
      ext.SetOption(EntitySessionOptions.DisableLog, !enable);
    }

    /// <summary>Enables/disables entity cache temporarily.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="enable">Optional, boolean flag indicating whether cache should enabled or disabled.</param>
    public static void EnableCache(this IEntitySession session, bool enable = true) {
      var ext = (EntitySession)session;
      ext.SetOption(EntitySessionOptions.DisableCache, !enable);
    }

    /// <summary>Returns the count of entities with pending changes not yet submitted to data store. </summary>
    /// <param name="session">Entity session.</param>
    /// <returns>Change count.</returns>
    public static int GetChangeCount(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.RecordsChanged.Count; 
    }

    //Helper method that can be used to create composite primary keys
    /// <summary>Creates a primary key object for an entity identified by type. Use it for entity types with composite primary key, to create a key instance from 
    /// column values. The created key may be used in session.GetEntity() method as primary key parameter. </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="session">The entity session.</param>
    /// <param name="values">Value(s) of the primary key columns.</param>
    /// <returns>A primary key object.</returns>
    public static object CreatePrimaryKey<TEntity>(this IEntitySession session, params object[] values) {
      Util.CheckParam(values, "values");
      var entInfo = session.Context.App.Model.GetEntityInfo(typeof(TEntity), throwIfNotFound: true);
      var pk = new EntityKey(entInfo.PrimaryKey, values);
      return pk; 
    }

    // TODO: reimplement with offset stored in Claims
    /*
    /// <summary>Returns user local time. The timezone offset value is in the user Principal and must be assigned before calling this method.</summary>
    /// <param name="context">Operation context, must have user sesssion established.</param>
    /// <param name="utcDateTime">Optional, UTC date/time to convert. If missing, function converts current UTC time.</param>
    /// <returns>User local time.</returns>
    public static DateTime GetUserLocalTime(this OperationContext context, DateTime? utcDateTime = null) {
      Util.Check(context.UserSession != null, "No user session established, cannot infer user local time.");
      var utc = utcDateTime == null ? context.App.TimeService.UtcNow : utcDateTime.Value;
      var shifted = utc.AddMinutes(context.UserSession.TimeZoneOffsetMinutes);
      var local = new DateTime(shifted.Year, shifted.Month, shifted.Day, shifted.Hour, shifted.Minute, shifted.Second, DateTimeKind.Unspecified);
      return local;
    }
    */

    public static TEntity NewCopyOf<TEntity>(this IEntitySession session, TEntity entity, bool copyPK = false) where TEntity : class {
      var copy = session.NewEntity<TEntity>();
      var entityInfo = EntityHelper.GetRecord(entity).EntityInfo;
      foreach(var member in entityInfo.Members) {
        switch(member.Kind) {
          case EntityMemberKind.Column:
            if(member.Flags.IsSet(EntityMemberFlags.PrimaryKey) && !copyPK)
              continue;
            var value = EntityHelper.GetProperty(entity, member.MemberName);
            EntityHelper.SetProperty(copy, member.MemberName, value);
            break;
            // do not copy entity references or lists
        }
      }
      return copy;
    }

    #region Transaction tags access
    /// <summary>Returns a set of tags set for the current/next transaction. </summary>
    /// <param name="session">Entity session.</param>
    /// <returns>A set of transaction tags defined.</returns>
    /// <remarks>The tags are free-form strings that can be used by client code to remember certain facts/values associated with SaveChanges operation. 
    /// They are NOT used in any database operations directly. The tags are cleared after session.SaveChanges() completes. 
    /// Example use: automatic scheduling of update queries in SavingChanges event for particular entity. 
    /// To avoid scheduling the same query multiple times, the client code can add a tag after scheduling the query for the first time, for ex: 
    /// [UpdateOrderTotalScheduled/orderid]. In consequitive invocations of the event handler the code can if the tag is already defined, so the query is already
    /// scheduled to run. 
    /// </remarks>
    public static HashSet<string> GetTransactionTags(this IEntitySession session) {
      var ext = (EntitySession)session;
      return ext.GetTransactionTags();
    }

    /// <summary>Returns true if a transaction tag is defined. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="tag">Tag value.</param>
    /// <returns>True if the tag is defined; otherwise, false.</returns>
    public static bool HasTransactionTag(this IEntitySession session, string tag) {
      var tags = GetTransactionTags(session);
      return tags.Contains(tag); 
    }

    /// <summary>Adds a transaction tag. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="tag">Tag value.</param>
    public static void AddTransactionTag(this IEntitySession session, string tag) {
      var tags = GetTransactionTags(session);
      tags.Add(tag);
    }

    /// <summary>Removes a transaction tag. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="tag">Tag value.</param>
    public static void RemoveTransactionTag(this IEntitySession session, string tag) {
      var tags = GetTransactionTags(session);
      if(tags.Contains(tag))
        tags.Remove(tag);
    }
    #endregion 

    public static bool IsSet(this EntitySessionOptions options, EntitySessionOptions option) {
      return (options & option) != 0;
    }

    public static TResult Select<TResult>(this IEntitySession session, Expression<Func<TResult>> expr) {
      var entSession = (EntitySession)session;
      var ctxEntSet = entSession.CreateDbContextEntitySet();
      // Create selector expr that can be accepted by Queryable.Select - add parameter for pseudo entity
      var prm = Expression.Parameter(typeof(INullEntity), "@NullTable");
      var selector = Expression.Lambda<Func<INullEntity, TResult>>(expr.Body, prm);
      var result = ctxEntSet.Select(selector).ToList().First();
      return result; 
    } 

    public static long GetSequenceNextValue(this IEntitySession session, SequenceDefinition sequence) {
      return session.Select(() => sequence.NextValue());
    }

  }//class

}
