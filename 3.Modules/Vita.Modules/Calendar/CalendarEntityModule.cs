using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Calendar {
  public class CalendarEntityModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public CalendarEntityModule(EntityArea area) : base(area, "Calendar", version: CurrentVersion) {
      RegisterEntities(typeof(ICalendar), typeof(ICalendarEvent));
    }
  }
}
