using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging {

  //Entities
  [Entity, OrderBy("CreatedOn:DESC"), ClusteredIndex("CreatedOn,Id")]
  public interface IErrorLog {
    [PrimaryKey]
    Guid Id { get; set; }

    //Note - we do not use Auto(AutoType.CreatedOn) attribute here - if we did, it would result in datetime
    // of record creation, which happens later (on background thread) than actual error. 
    // So it should be set explicitly in each case, when the log call is made
    [Utc, Index]
    DateTime CreatedOn { get; set; }

    [Size(Sizes.UserName)]
    string UserName { get; set; }

    [Index]
    Guid? UserId { get; set; }
    long? AltUserId { get; set; }

    Guid? UserSessionId { get; set; }

    Guid? WebCallId { get; set; }

    [Size(Sizes.Name), Nullable]
    string MachineName { get; set; }

    [Size(Sizes.Name), Nullable]
    string AppName { get; set; }

    ErrorKind Kind { get; set; }

    [Size(250)]
    string Message { get; set; }

    [Unlimited, Nullable]
    string Details { get; set; }

    [Unlimited, Nullable]
    string OperationLog { get; set; }

  }

}
  