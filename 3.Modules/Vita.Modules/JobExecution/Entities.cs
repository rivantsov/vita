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
    Guid Id { get; set; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreateOn { get; }

    [Size(Sizes.Name)]
    string Code { get; set; }
    ThreadType ThreadType { get; set; }
    JobFlags Flags { get; set; }
    [Nullable]
    IJob ParentJob { get; set; }
    [Size(250)]
    string TargetType { get; set; }
    [Size(100)]
    string TargetMethod { get; set; }
    int TargetParameterCount { get; set; }
    [Nullable, Unlimited]
    string Arguments { get; set; }

    int RetryIntervalSec { get; set; }
    int RetryCount { get; set; }
    int RetryPauseMinutes { get; set; }
    int RetryRoundsCount { get; set; }

  }

  [Entity, BypassAuthorization]
  public interface IJobRun {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Utc, Auto(AutoType.CreatedOn)]
    DateTime CreatedOn { get; }

    IJob Job { get; set; }
    Guid? SourceId { get; set; }
    JobRunStatus Status { get; set; }
    [Utc]
    DateTime LastStartedOn { get; set; }
    [Utc]
    DateTime? LastEndedOn { get; set; }
    [Utc]
    DateTime? NextStartOn { get; set; }
    int RunCount { get; set; }
    int RemainingRetries { get; set; }
    int RemainingRounds { get; set; }
    [Nullable, Unlimited]
    string CurrentArguments { get; set; }

    double Progress { get; set; }
    [Nullable, Size(200)]
    string ProgressMessage { get; set; }
    //Concatenation of ProgressMessages separated by NewLine 
    [Nullable, Unlimited]
    string Log { get; set; }
    [Nullable, Unlimited]
    string Errors { get; set; }


  }

}
