using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.Calendar {

  [Entity]
  public interface IEvent {
    [PrimaryKey, Auto]
    Guid Id { get; }
    Guid? OwnerId { get; set; }
    [GrantAccess]
    IEventTemplate Template { get; set; }
    EventStatus Status { get; set; }
    [Utc]
    DateTime StartOn { get; set; }
    //In case StartOn changed, holds original value
    [Utc, Index]
    DateTime OriginalStartOn { get; set; }
    [Utc, Index]
    DateTime? NextActivateOn { get; set; }
    [Unlimited, Nullable]
    string Log { get; set; }
  }

  [Entity, Unique("Code,OwnerId")]
  public interface IEventTemplate {
    [PrimaryKey, Auto]
    Guid Id { get; }
    EventFlags Flags { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    [Size(Sizes.LongName)]
    string Title { get; set; }
    [Unlimited]
    string Description { get; set; }
    // for events with duration, like meetings
    int DurationMinutes { get; set; }
    Guid? OwnerId { get; set; }

    [Nullable]
    IJob JobToRun { get; set; }

    // Free-form custom data 
    Guid? CustomId { get; set; }
    [Nullable, Unlimited]
    string CustomData { get; set; }
    IList<IEventSubEvent> SubEvents { get; }
  }

  [Entity]
  public interface IEventSchedule {
    [PrimaryKey, Auto]
    Guid Id { get; }
    DateTime ActiveFrom { get; set; }
    DateTime? ActiveUntil { get; set; }
    ScheduleStatus Status { get; set; }
    [GrantAccess]
    IEventTemplate Template { get; set; }
    // Schedule
    [Size(100), Nullable]
    string ScheduleCronSpec { get; set; }
    [Utc, Index]
    DateTime? NextStartOn { get; set; }
    [Utc, Index]
    DateTime? NextActivateOn { get; set; }

  }

  /// <summary>Sub-event related to main event, for ex: email notificaiton 10 minutes before the meeting (main event).
  /// </summary>
  [Entity]
  public interface IEventSubEvent {
    [PrimaryKey, Auto]
    Guid Id { get; }
    [CascadeDelete]
    IEventTemplate Event { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    [Nullable, Size(Sizes.Description)]
    string Title { get; set; }
    int OffsetMinutes { get; set; }
    [Nullable]
    IJob JobToRun { get; set; }
  }


} //ns
