using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

using Vita.Common;
using Vita.Entities.Runtime;
using Vita.Entities.Model;

namespace Vita.Entities.Authorization {
  

  /// <summary>An interface exposing access rights for an entity. Use <c>EntityHelper</c></summary>
  public interface IEntityAccess {

    bool IsAuthorizationEnabled {get;}

    bool CanPeek<TEntity>(Expression<Func<TEntity, object>> memberSelector);
    bool CanPeek(string memberName);
    bool CanPeek();
    bool CanRead<TEntity>(Expression<Func<TEntity, object>> memberSelector);
    bool CanRead(string memberName);
    bool CanRead();
    bool CanUpdate<TEntity>(Expression<Func<TEntity, object>> memberSelector);
    bool CanUpdate(string memberName);
    bool CanUpdate();
    bool CanDelete();
  }

}
