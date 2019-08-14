using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {

  public static class LogEntityExtensions {
    public static TEntity NewLogEntity<TEntity>(this IEntitySession session, LogEntry entry, ILogUserInfo user) where TEntity : class, ILogEntityBase {
      var ent = session.NewEntity<TEntity>();
      ent.User = user; 
      ent.CreatedOn = entry.CreatedOn;
      ent.Id = entry.Id;
      ent.SessionId = entry.Context.SessionId;
      ent.WebCallId = entry.Context.WebCallId;

      return ent;
    }

    public static ILogUserInfo NewLogUserInfo(this IEntitySession session, LogContext logContext) {
      var ent = session.NewEntity<ILogUserInfo>();
      ent.UserId = logContext.User.UserId;
      ent.AltUserId = logContext.User.AltUserId;
      ent.UserName = logContext.User.UserName;
      return ent; 
    }

  }
}
