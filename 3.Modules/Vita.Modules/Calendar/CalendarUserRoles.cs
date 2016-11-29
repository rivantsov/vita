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
      var calendarData = new EntityGroupResource("Calendar", 
        typeof(IEventTemplate), typeof(IEventSubEvent), typeof(IEvent));
      var userFilter = new AuthorizationFilter("CalendarUse");
      userFilter.Add<IEvent, Guid>((e, userid) => e.OwnerId == userid);
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
