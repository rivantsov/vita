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

  public enum CalendarEventStatus {
    NotStarted = 0,
    LeadFired = 1, 
    Fired = 2,
    Canceled = 3,
    Error = 4, 
  }

  public enum CalendarEventSeriesStatus {
    Active,
    Suspended, 
  }

  [Flags]
  public enum CalendarEventFlags {
    None = 0,
    // For personal calendars indicates if event is visible to the user (it might be system-scheduled, hidden event)
    UserVisible = 1, 
  }

}
