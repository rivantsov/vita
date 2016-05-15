using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities.Runtime;
using Vita.Entities.Model;
using System.Net.Http;
using System.Linq.Expressions;

namespace Vita.Entities.Authorization {

  public static class AuthorizationModelExtensions {
    public static bool IsSet(this AccessType types, AccessType type) {
      return (types & type) != 0;
    }
    public static bool IsSet(this FilterUse flags, FilterUse flag) {
      return (flags & flag) != 0;
    }


    public static AccessType GetRequiredAccessType(this EntityStatus status) {
      switch (status) {
        case EntityStatus.New: return AccessType.CreateStrict;
        case EntityStatus.Modified: return AccessType.UpdateStrict;
        case EntityStatus.Deleting: return AccessType.DeleteStrict;
        default: return AccessType.Peek;
      }
    }

    public static AccessType ToAccessType(this ReadAccessLevel level) {
      return (level == ReadAccessLevel.Read) ? AccessType.ReadStrict : AccessType.Peek;
    }

    public static AccessType GetAuthorizationAccessType(this EntityCommandKind commandKind) {
      switch (commandKind) {
        case EntityCommandKind.SelectAll:
        case EntityCommandKind.SelectAllPaged:
        case EntityCommandKind.SelectByKey:
        case EntityCommandKind.SelectByKeyManyToMany:
        case EntityCommandKind.SelectByKeyArray:
        case EntityCommandKind.CustomSelect:
          return AccessType.Peek;
        case EntityCommandKind.Update:
        case EntityCommandKind.CustomUpdate:
        case EntityCommandKind.PartialUpdate:
          return AccessType.Update;
        case EntityCommandKind.Insert:
        case EntityCommandKind.CustomInsert:
          return AccessType.Create;
        case EntityCommandKind.Delete:
        case EntityCommandKind.CustomDelete:
          return AccessType.Delete;
        default:
          return AccessType.None;
      }
    }//method

    public static AccessType GetAccessType(string httpMethod) {
      switch(httpMethod) {
        case "GET": return AccessType.ApiGet;
        case "POST": return AccessType.ApiPost;
        case "PUT": return AccessType.ApiPut;
        case "DELETE": return AccessType.ApiDelete;
        default:
          return AccessType.None;
      }
    }

    public static bool Exists<TEntity>(this OperationContext context, 
                                      [DoNotRewrite] Expression<Func<TEntity, bool>> predicate) 
                                      where TEntity : class {
      var session = context.OpenSystemSession();
      session.EnableCache(false); 
      var count = session.EntitySet<TEntity>().Where(predicate).Count();
      return count > 0;
    }
    //This is more efficient - uses sparse cache (if entity is in sparse cashe)
    public static bool ExistsWithKey<TEntity>(this OperationContext context, params object[] primaryKey) where TEntity : class {
      var session = context.OpenSystemSession();
      var pk = session.CreatePrimaryKey<TEntity>(primaryKey);
      var ent = session.GetEntity<TEntity>(pk);
      return ent != null;
    }

  }//class

}
