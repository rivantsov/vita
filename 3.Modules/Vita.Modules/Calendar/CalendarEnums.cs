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
    Fired = 1,
    Canceled = 2,
  }

  [Flags]
  public enum CalendarEventFlags {
    None = 0,
  }

}
