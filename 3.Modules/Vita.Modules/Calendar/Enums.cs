using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Calendar {

  public enum CalendarType {
    System,
    Group,
    Individual,
  }

  public enum EventStatus {
    Pending = 0,
    Firing = 1,
    Completed = 3,
    Error = 5, 
    Canceled = 6,
  }

  public enum ScheduleStatus {
    Active,
    Stopped, 
  }

  [Flags]
  public enum EventFlags {
    None = 0,
    /// <summary>Indicates that IEventInfo is a customized version, for specific occurrence, 
    /// of a repeated generic event with a schedule.</summary>
    IsCustomizedVersion = 1, 
    // For personal calendars indicates if event is visible to the user (it might be system-scheduled, hidden event)
    UserVisible = 4, 
  }

}
