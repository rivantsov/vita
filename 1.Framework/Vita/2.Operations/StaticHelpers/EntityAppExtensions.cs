using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Runtime;

namespace Vita.Entities {

  public static class EntityAppExtensions {

    /// <summary>Opens an entity session for an anonymous user. </summary>
    /// <param name="app">Entity app instance.</param>
    /// <returns>An entity session.</returns>
    public static IEntitySession OpenSession(this EntityApp app) {
      var context = new OperationContext(app, UserInfo.Anonymous);
      return new EntitySession(context);
    }

    public static IEntitySession OpenSession(this OperationContext context) {
      return new EntitySession(context);
    }

    public static IEntitySession OpenSystemSession(this OperationContext context) {
      if(context.User.Kind == UserKind.System)
        return context.OpenSession();
      var sysCtx = new OperationContext(context.App, UserInfo.System, context.WebContext);
      return sysCtx.OpenSession();
    }


    public static IEntitySession OpenSystemSession(this EntityApp app) {
      Util.CheckParam(app, nameof(app));
      var ctx = app.CreateSystemContext();
      return ctx.OpenSession();
    }
    /// <summary>Opens entity session (not secure). Authorization is not implemented in VITA 2.0 (net core), 
    /// so no secure sessions; this method is kept to avoid breaking existing code. </summary>
    /// <param name="context">Operation context.</param>
    /// <returns>Entity session.</returns>
    public static IEntitySession OpenSecureSession(this OperationContext context) {
      return new EntitySession(context);
    }
    public static OperationContext CreateSystemContext(this EntityApp app) {
      var ctx = new OperationContext(app, UserInfo.System);
      return ctx;
    }


    public static string GetIpAddress(this OperationContext context) {
      if(context == null || context.WebContext == null)
        return null;
      return context.WebContext.Request.IPAddress;
    }

    /// <summary>Checks if an entity is registered with entity model. 
    /// For use in customizable models when several versions might exist for different environments, 
    /// and some entities are excluded in some models.</summary>
    /// <param name="app">Entity app.</param>
    /// <param name="entityType">The type of entity to check.</param>
    /// <returns>True if the entity is part of the model; otherwise, false.</returns>
    public static bool IsRegisteredEntity(this EntityApp app, Type entityType) {
      return app.Model.EntitiesByType.ContainsKey(entityType);
    }

    public static Vita.Data.Runtime.Database GetDefaultDatabase(this EntityApp app) {
      return app.DataAccess.GetDataSources().FirstOrDefault()?.Database;
    }

  }
}
