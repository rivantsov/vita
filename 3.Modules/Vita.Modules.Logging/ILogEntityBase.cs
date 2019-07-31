using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {
  //Base class for most log entities
  [Entities.DoNotTrack]
  public interface ILogEntityBase {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    //Note - we do not use Auto(AutoType.CreatedOn) attribute here - if we did, it would result in datetime
    // of record creation, which happens later (on background thread) than actual event. 
    // So it should be set explicitly in each case, when the log call is made
    [Utc, Index]  
    DateTime CreatedOn { get; set; }
    [Nullable, Size(Sizes.UserName)]
    //useful when there's no user session; also helps to have username directly in the table when looking 
    // at raw tables in SQL window
    string UserName { get; set; } 
    [Index]
    Guid? UserSessionId { get; set; }
    [Index]
    Guid? WebCallId { get; set; }
  
  }


  internal static class LoggingExtensions {
    public static void Init(this LogEntry log, OperationContext context) {
      log.CreatedOn = context.App.TimeService.UtcNow;
      log.UserName = context.User.UserName;
      if (context.UserSession != null)
        log.UserSessionId = context.UserSession.SessionId;
      if (context.WebContext != null)
        log.WebCallId = context.WebContext.Id; 
    }

    public static TEntity NewLogEntity<TEntity>(this IEntitySession session, LogEntry log) 
                              where TEntity: class, ILogEntityBase {
      var ent = session.NewEntity<TEntity>();
      if (log.Id != null)
        ent.Id = log.Id.Value;
      ent.CreatedOn = log.CreatedOn;
      ent.UserName = log.UserName;
      ent.UserSessionId = log.UserSessionId;
      ent.WebCallId = log.WebCallId;
      return ent; 
    }
  }//class
}
