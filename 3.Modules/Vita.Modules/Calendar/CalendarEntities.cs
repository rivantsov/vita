using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.Calendar {

  [Entity]
  public interface ICalendar {
    [PrimaryKey, Auto]
    Guid Id { get; }

    [Auto(AutoType.CreatedOn)]
    DateTime CreatedOn { get; }

    CalendarType Type { get; set; }

    [Size(Sizes.Name)]
    string Name { get; set; }

    Guid? OwnerId { get; set; } //org, group or user
  }

  [Entity]
  public interface ICalendarEvent {
    [PrimaryKey, Auto]
    Guid Id { get; }
    ICalendar Calendar { get; set; }
    CalendarEventFlags Flags { get; set; }
    CalendarEventStatus Status { get; set; }

    DateTime RunOn { get; set; }
    DateTime LeadTime { get; set; }
    int DurationMinutes { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    [Size(Sizes.LongName)]
    string Title { get; set; }
    [Unlimited]
    string Description { get; set; }
    [Unlimited, Nullable]
    string ExecutionNotes { get; set; }

    [Nullable]
    IJob JobToRun { get; set; }

    [Nullable]
    ICalendarEventSeries Series { get; set; }
    // originally scheduled to run on from series schedule
    DateTime? ScheduledRunOn { get; set; }

    // Free-form parameters 
    Guid? CustomItemId { get; set; }
    [Nullable, Unlimited]
    string CustomData { get; set; }
  }

  [Entity]
  public interface ICalendarEventSeries {
    [PrimaryKey, Auto]
    Guid Id { get; }
    ICalendar Calendar { get; set; }
    CalendarEventSeriesStatus Status { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    [Size(Sizes.LongName)]
    string Title { get; set; }
    [Unlimited]
    string Description { get; set; }
    [Size(100), Nullable]
    string CronSpec { get; set; }
    int LeadInterval { get; set; }
    DateTime? LastRunOn { get; set; }
    [Index]
    DateTime? NextRunOn { get; set; }
    [Index]
    DateTime? NextLeadTime { get; set; }

    [Nullable]
    IJob JobToRun { get; set; }
  }


} //ns
