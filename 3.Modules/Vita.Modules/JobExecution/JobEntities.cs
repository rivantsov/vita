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
    [Auto(AutoType.CreatedById)]
    Guid CreatedBy { get; }

    // Definition
    [Size(Sizes.Name), Index]
    string Name { get; set; }
    JobFlags Flags { get; set; }

    // Launch parameters
    [Size(50), Nullable]
    string HostName { get; set; }
    JobThreadType ThreadType { get; set; }
    [Size(250)]
    string DeclaringType { get; set; }
    [Size(100)]
    string MethodName { get; set; }
    int MethodParameterCount { get; set; }
    [Nullable, Unlimited]
    string Arguments { get; set; }

    // ex: "1,1,30,360,360", means retry after 1 minute, again after 1 min, then after 30m, then after 6h, again after 6h
    [Nullable, Size(50)]
    string RetryIntervals { get; set; } 

    [Nullable, OneToOne]
    IJobSchedule Schedule { get; }
  }

  [Entity, BypassAuthorization]
  public interface IJobRun {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Utc, Auto(AutoType.CreatedOn)]
    DateTime CreatedOn { get; }
    Guid? UserId { get; set; }

    IJob Job { get; set; }
    Guid? DataId { get; set; }
    [Unlimited, Nullable]
    string Data { get; set; }

    JobRunStatus Status { get; set; }
    int AttemptNumber { get; set; }

    [Utc]
    DateTime StartOn { get; set; }
    [Utc]
    DateTime? StartedOn { get; set; }
    [Utc]
    DateTime? EndedOn { get; set; }

    double Progress { get; set; }
    [Nullable, Size(200)]
    string ProgressMessage { get; set; }
    //Concatenation of ProgressMessages separated by NewLine; also error message 
    [Nullable, Unlimited]
    string Log { get; set; }
  }



  // New Stuff, joining EventScheduling and JobExecution modules =======================================
  public enum JobScheduleStatus {
    Active,
    Stopped,
  }

  [Entity, BypassAuthorization, Display("CRON: {CronSpec}")]
  public interface IJobSchedule {
    [PrimaryKey]
    IJob Job { get; set; }
    [Size(Sizes.Name)]
    string Name { get; set; }
    JobScheduleStatus Status { get; set; }
    DateTime ActiveFrom { get; set; }
    DateTime? ActiveUntil { get; set; }
    // Schedule
    [Size(100)]
    string CronSpec { get; set; }
    // Id of IJobRun 
    Guid? NextRunId { get; set; }
  }

}
