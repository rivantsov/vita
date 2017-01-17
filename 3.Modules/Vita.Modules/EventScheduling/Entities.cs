using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.EventScheduling {

  [Entity, BypassAuthorization, Display("{Code}: {StartOn}")]
  public interface IEvent {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [GrantAccess]
    IEventInfo EventInfo { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    EventStatus Status { get; set; }
    [Utc]
    DateTime StartOn { get; set; }
    [Unlimited, Nullable]
    string Log { get; set; }
  }

  [Entity, BypassAuthorization, Display("{Code}")]
  public interface IEventInfo {
    [PrimaryKey, Auto]
    Guid Id { get; }
    EventFlags Flags { get; set; }
    [Size(Sizes.Name), Index]
    string Code { get; set; }
    [Size(Sizes.LongName)]
    string Title { get; set; }
    [Unlimited, Nullable]
    string Description { get; set; }
    [OneToOne]
    IEventSchedule Schedule { get; }

    [Nullable]
    IJob JobToRun { get; set; }

    // Free-form custom data 
    Guid? OwnerId { get; set; }
    Guid? DataId { get; set; }
    [Nullable, Unlimited]
    string Data { get; set; }
  }
  
  [Entity, BypassAuthorization, Display("{EventInfo.Code}: {CronSpec}")]
  public interface IEventSchedule {
    [PrimaryKey]
    IEventInfo EventInfo { get; set; }
    DateTime ActiveFrom { get; set; }
    DateTime? ActiveUntil { get; set; }
    ScheduleStatus Status { get; set; }
    // Schedule
    [Size(100)]
    string CronSpec { get; set; }
    [Utc]
    DateTime? LastStartedOn { get; set; }
    [Utc, Index]
    DateTime? NextStartOn { get; set; }
  }

} //ns
