using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.JobExecution {

  [Entity, BypassAuthorization]
  public interface IJob {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [Auto(AutoType.CreatedOn)]
    DateTime CreateOn { get; }

    [Size(Sizes.Name)]
    string Code { get; set; }
    JobFlags Flags { get; set; }
    JobStatus Status { get; set; }
    Guid? OwnerId { get; set; }

    string TargetType { get; set; }
    string TargetMethod { get; set; }
    int TargetParameterCount { get; set; }
    [Nullable, Unlimited]
    string SerializedArguments { get; set; }

    int RetryCount { get; set; }
    int RetryIntervalMinutes { get; set; }
    [Index]
    DateTime? NextRunOn { get; set; }
    DateTime? LastRunOn { get; set; }
    DateTime? CompletedOn { get; set; }
    [Nullable, Unlimited]
    string Errors { get; set; }

    [Nullable]
    IJob ExecuteAfterJob { get; set; }

    //long-running props
    double Progress { get; set; }

  }

}
