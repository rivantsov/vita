using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Logging {

  //Entities
  [Entity, OrderBy("CreatedOn:DESC"), ClusteredIndex("CreatedOn,Id"), DoNotTrack]
  public interface IErrorLog {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    //Note - we do not use Auto(AutoType.CreatedOn) attribute here - if we did, it would result in datetime
    // of record creation, which happens later (on background thread) than actual event. 
    // So it should be set explicitly in each case, when the log call is made
    [Utc, Index]
    DateTime CreatedOn { get; set; }

    [Nullable, Size(Sizes.UserName)]
    string UserName { get; set; }

    [Index]
    Guid? UserSessionId { get; set; }

    [Index]
    Guid? WebCallId { get; set; }

    [Size(50), Nullable]
    string MachineName { get; set; }

    [Size(50), Nullable, Index]
    string AppName { get; set; }
    [Size(250)]
    string Message { get; set; }

    [Unlimited, Nullable]
    string Details { get; set; }
    [Unlimited, Nullable]
    string OperationLog { get; set; }

    bool IsClientError { get; set; }
  }

}
  