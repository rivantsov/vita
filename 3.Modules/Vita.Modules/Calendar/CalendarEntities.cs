using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.Party;

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

    [Nullable]
    IParty Owner { get; set; } //org, group or user
  }

  [Entity]
  public interface ICalendarEvent {
    [PrimaryKey, Auto]
    Guid Id { get; }
    ICalendar Calendar { get; set; }
    [Nullable]
    ICalendarEventStream EventStream { get; set; }
    DateTime StartsOn { get; set; }
    DateTime LeadTime { get; set; }
    int DurationMinutes { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    [Size(Sizes.LongName)]
    string Title { get; set; }
    [Unlimited]
    string Description { get; set; }
    CalendarEventStatus Status { get; set; }
    CalendarEventFlags Flags { get; set; }
    [Unlimited, Nullable]
    string ExecutionNotes { get; set; }
  }

  [Entity]
  public interface ICalendarEventStream {
    [PrimaryKey, Auto]
    Guid Id { get; }
    ICalendar Calendar { get; set; }
    [Size(Sizes.Name)]
    string Code { get; set; }
    [Size(Sizes.LongName)]
    string Title { get; set; }
    [Unlimited]
    string Description { get; set; }
    [Size(100), Nullable]
    string CronSpec { get; set; }
    DateTime? LastRunOn { get; set; }
    DateTime? NextRunOn { get; set; }
  }


} //ns
