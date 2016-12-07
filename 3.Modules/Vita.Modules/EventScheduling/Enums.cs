using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.EventScheduling {

  public enum EventStatus {
    Pending = 0,
    Fired = 1,
    Completed = 3,
    Error = 5, 
    Canceled = 6,
  }

  public enum ScheduleStatus {
    Active,
    Stopped, 
  }

  // Reserved for future
  [Flags]
  public enum EventFlags {
    None = 0,
  }

}
