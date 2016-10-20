using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Authorization; 

namespace Vita.Modules.Calendar {
  public class CalendarUserRoles {
    public Role RegularUser;
    public Role Administrator; 

    internal CalendarUserRoles() {
      var calendarData = new EntityGroupResource("Calendar", typeof(ICalendar), typeof(ICalendarEventSeries), typeof(ICalendarEvent));
      var userFilter = new AuthorizationFilter("CalendarUse");
      userFilter.Add<ICalendar, Guid>((c, userid) => c.OwnerId == userid);
      userFilter.Add<ICalendarEvent, Guid>((ce, userid) => ce.Calendar.OwnerId == userid);
      userFilter.Add<ICalendarEventSeries, Guid>((s, userid) => s.Calendar.OwnerId == userid);
      var editCalPerm = new EntityGroupPermission("EditCalendar", AccessType.CRUD, calendarData);
      var useCalendar = new Activity("UseCalendar", editCalPerm);
      RegularUser = new Role("CalendarUser");
      RegularUser.Grant(userFilter, useCalendar);

      // Admin
      Administrator = new Role("CalendarAdmin");
      Administrator.Grant(editCalPerm);
    }

  }
}
