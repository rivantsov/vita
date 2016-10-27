using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data;
using Vita.Entities.Authorization;
using Vita.Entities.Linq;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Entities {

  public static partial class EntitySessionExtensions {

    public static IEntitySession OpenSession(this OperationContext context) {
      var session = new EntitySession(context);
      return session; 
    }

    public static ISecureSession OpenSecureSession(this OperationContext context) {
      var session = new SecureSession(context);
      return session;
    }

    public static IEntitySession OpenSystemSession(this OperationContext context) {
      if(context.User.Kind == UserKind.System)
        return context.OpenSession();
      var sysContext = new OperationContext(context, UserInfo.System);
      return sysContext.OpenSession(); 
    }

    public static IEntitySession OpenSystemSession(this EntityApp app) {
      Util.Check(app != null, "App may not be null.");
      var sysContext = new OperationContext(app, UserInfo.System);
      return new EntitySession(sysContext); 
    }
    public static OperationContext CreateSystemContext(this EntityApp app) {
      Util.Check(app != null, "App may not be null.");
      var sysContext = new OperationContext(app, UserInfo.System);
      return sysContext;
    }
    /// <summary>Opens entity session.</summary>
    /// <param name="app">Entity app.</param>
    /// <param name="user">User info, optional.</param>
    /// <returns>Entity session instance.</returns>
    public static IEntitySession OpenSession(this EntityApp app, UserInfo user = null) {
      Util.Check(app != null, "App may not be null.");
      user = user ?? UserInfo.Anonymous;
      var anonContext = new OperationContext(app, user);
      return new EntitySession(anonContext);
    }

    /// <summary>Disables use of stored procedures and forces all database commands to be executed using parameterized SQL. </summary>
    /// <param name="session">Entity session.</param>
    public static void DisableStoredProcs(this IEntitySession session) {
      var entSession = (EntitySession)session;
      entSession.Options |= EntitySessionOptions.DisableStoredProcs;
    }
    /// <summary>Disables use of batch mode for updates </summary>
    /// <param name="session">Entity session.</param>
    public static void DisableBatchMode(this IEntitySession session) {
      var entSession = (EntitySession)session;
      entSession.Options |= EntitySessionOptions.DisableBatch;
    }

    /// <summary>Checks if an entity is registered with entity model. 
    /// For use in customizable models when several versions might exist for different environments, 
    /// and some entities are excluded in some models.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="entityType">The type of entity to check.</param>
    /// <returns>True if the entity is part of the model; otherwise, false.</returns>
    public static bool IsRegisteredEntity(this IEntitySession session, Type entityType) {
      var entSession = (EntitySession)session;
      var metaInfo = entSession.Context.App.Model.GetEntityInfo(entityType);
      return (metaInfo != null);
    }

    public static void LogMessage(this IEntitySession session, string message, params object[] args) {
      var entSession = (EntitySession)session; 
      entSession.AddLogEntry(new InfoLogEntry(session.Context, message, args));
    }

    public static string GetLogContents(this OperationContext context) {
      if(context == null || context.LocalLog == null)
        return null;
      return context.LocalLog.GetAllAsText(); 
    }
    public static string GetIpAddress(this OperationContext context) {
      if(context == null || context.WebContext == null)
        return null;
      return context.WebContext.IPAddress;
    }

    public static LinqCommand GetLastLinqCommand(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.LastLinqCommand;
    }
    public static Guid GetNextTransactionId(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.NextTransactionId;
    }

    public static System.Data.IDbCommand GetLastCommand(this IEntitySession session) {
      var entSession = (EntitySession)session; 
      return entSession.LastCommand;
    }

    //Used in Search to set main query command as LAST - it is overridden by Total query
    internal static void SetLastCommand(this IEntitySession session, System.Data.IDbCommand command) {
      var entSession = (EntitySession)session;
      entSession.LastCommand = command;
    }

    public static IDisposable WithElevateRead(this IEntitySession session) {
      var entSession = (EntitySession)session;
      return entSession.ElevateRead(); 
    }

    public static void SetOutgoingCookie(this OperationContext context, string name, string value) {
      var webCtx = context.WebContext;
      if(webCtx != null)
        webCtx.OutgoingCookies.Add(new System.Net.Cookie(name, value));
    }


    public static Vita.Data.IDirectDbConnector GetDirectDbConnector(this IEntitySession session, bool admin = false) {
      var entSession = (EntitySession)session;
      var conn = entSession.CurrentConnection;
      if (conn == null) {
        var ds = session.Context.App.DataAccess.GetDataSource(session.Context);
        conn = entSession.CurrentConnection = ds.Database.GetConnection(entSession, 
           ConnectionLifetime.Explicit, admin: admin);
      }  
      conn.Lifetime = ConnectionLifetime.Explicit; 
      return conn;
    }

    /// <summary>Enables/disables local log temporarily.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="enable">Optional, boolean flag indicating whether log should enabled or disabled.</param>
    public static void EnableLog(this IEntitySession session, bool enable = true) {
      var entSession = (EntitySession)session;
      entSession.LogDisabled = !enable;
    }

    /// <summary>Enables/disables entity cache temporarily.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="enable">Optional, boolean flag indicating whether cache should enabled or disabled.</param>
    public static void EnableCache(this IEntitySession session, bool enable = true) {
      var entSession = (EntitySession)session;
      entSession.CacheDisabled = !enable;
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
    public static EntityKey CreatePrimaryKey<TEntity>(this IEntitySession session, params object[] values) {
      var entSession = session as EntitySession;
      var modelInfo = entSession.Context.App.Model;
      var entInfo = modelInfo.GetEntityInfo(typeof(TEntity), true);
      var key = EntityKey.CreateSafe(entInfo.PrimaryKey, values);
      return key;
    }

    /// <summary>Retrieves a list of child entities for a given parent entity.</summary>
    /// <typeparam name="TParentEntity">Parent entity type, the type of a reference property of the child entity. </typeparam>
    /// <typeparam name="TEntity">Child entity type.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="parent">Parent entity.</param>
    /// <param name="parentRefProperty">Optional. The name of the property of child entity that references the parent entity. Use it when
    /// there is more than one such property.</param>
    /// <returns>A list of child entities that reference the parent entity.</returns>
    public static IList<TEntity> GetChildEntities<TParentEntity, TEntity>(this IEntitySession session,
                                                                          TParentEntity parent, string parentRefProperty = null)
                                                                          where TEntity : class {
      var entSession = (EntitySession)session;
      Util.Check(parent != null, "GetChildEntities<{0},{1}>: parent parameter may not be null.", typeof(TParentEntity), typeof(TEntity));
      var parentRec = EntityHelper.GetRecord(parent);
      Util.Check(parentRec != null, "GetChildEntities<{0},{1}>: parent parameter ({2}) is not an entity.", typeof(TParentEntity), typeof(TEntity), parent);
      var parentEntity = parentRec.EntityInfo;
      var childEntity = entSession.GetEntityInfo(typeof(TEntity));
      Util.Check(childEntity != null, "Entity {0} not registered with the entity model.", typeof(TEntity));
      EntityMemberInfo member;
      if(string.IsNullOrEmpty(parentRefProperty)) {
        var members = childEntity.Members.FindAll(m => m.DataType == typeof(TParentEntity));
        Util.Check(members.Count > 0, "Failed to select child entities. Reference to entity {0} not found on entity {1}.", typeof(TParentEntity), typeof(TEntity));
        Util.Check(members.Count < 2, "Failed to select child entities. More than one reference to entity {0} found on entity {1}. Explicitly specify property to use.",
          typeof(TParentEntity), typeof(TEntity));
        member = members[0];
      } else {
        member = childEntity.GetMember(parentRefProperty);
        Util.Check(member != null, "Failed to select child entities. Reference to entity {0} not found on entity {1}.", typeof(TParentEntity), typeof(TEntity));
        var refOk = member.Kind == MemberKind.EntityRef && member.ReferenceInfo.ToKey.Entity == parentEntity;
        Util.Check(refOk, "Failed to select child entities. Property {0} on entity {1} does not reference entity {2}.",
          parentRefProperty, typeof(TEntity), typeof(TParentEntity));
      }
      //actually retrieve entities
      var records = entSession.GetChildRecords(parentRec, member);
      return entSession.ToEntities<TEntity>(records);
    }

    public static void ExecuteNonQuery(this IEntitySession session, string sql, params object[] args) {
      var sqlStmt = string.Format(sql, args);
      var dbConn = session.GetDirectDbConnector().DbConnection;
      var cmd = dbConn.CreateCommand();
      cmd.CommandText = sqlStmt;
      var isClosed = dbConn.State != System.Data.ConnectionState.Open;
      if (isClosed)
        dbConn.Open(); 
      cmd.ExecuteNonQuery();
      if (isClosed)
        dbConn.Close(); 
    }

    public static T GetSequenceNextValue<T>(this IEntitySession session, string sequenceName) where T: struct {
      var seq = session.Context.App.Model.FindSequence(sequenceName);
      Util.Check(seq != null, "Sequence {0} not found.", sequenceName);
      return GetSequenceNextValue<T>(session, seq); 
    }

    public static T GetSequenceNextValue<T>(this IEntitySession session, SequenceDefinition sequence)
       where T: struct {
      Util.Check(sequence != null, "Sequence parameter may not be null.");
      Util.Check(typeof(T) == sequence.DataType, "Requested next value type {0} does not match sequence {1} data type (2}.",
          typeof(T), sequence.Name, sequence.DataType);
      var ds = session.Context.App.DataAccess.GetDataSource(session.Context);
      var db = ds.Database;
      var entSession = (EntitySession)session;
      var v = db.GetSequenceNextValue(entSession, sequence);
      if (v.GetType() == typeof(T))
        return (T) v;
      //Postgres - all sequences are int64, so we need to convert here
      return (T) ConvertHelper.ChangeType(v, typeof(T));
    }

    public static DateTime GetUserLocalTime(this OperationContext context, DateTime? utcDateTime = null) {
      Util.Check(context.UserSession != null, "No user session established, cannot infer user local time.");
      var utc = utcDateTime == null ? context.App.TimeService.UtcNow : utcDateTime.Value;
      var shifted = utc.AddMinutes(context.UserSession.TimeZoneOffsetMinutes);
      var local = new DateTime(shifted.Year, shifted.Month, shifted.Day, shifted.Hour, shifted.Minute, shifted.Second, DateTimeKind.Unspecified);
      return local;
    }

    public static bool IsSet(this EntitySessionOptions options, EntitySessionOptions option) {
      return (options & option) != 0;
    }

  }//class
}
