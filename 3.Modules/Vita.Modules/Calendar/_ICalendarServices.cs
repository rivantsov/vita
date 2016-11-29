using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.Calendar {

  public class SubEventInfo {
    public Guid Id;
    public string Code;
    public string Title; 
    public int OffsetMinutes; 
  }

  public class CalendarEventArgs : EventArgs {
    public Guid EventId;
    public Guid EventTemplateId;
    public Guid? OwnerId;
    public string Code;
    public string Title;
    public DateTime StartOn;
    
    public EventStatus Status;
    public string Log;
    // free-form parameters
    public Guid? CustomItemId;
    public string CustomData;

    public SubEventInfo SubEvent; 

    public CalendarEventArgs(IEvent evt, IEventSubEvent subEvent = null) {
      EventId = evt.Id;
      var template = evt.Template;
      EventTemplateId = template.Id; 
      Code = template.Code;
      Title = template.Title;
      OwnerId = evt.OwnerId;
      Status = evt.Status;
      StartOn = evt.StartOn;
      CustomItemId = template.CustomId;
      CustomData = template.CustomData;
      if(subEvent != null)
        SubEvent = new SubEventInfo() { Id = subEvent.Id, Code = subEvent.Code, Title = subEvent.Title,
             OffsetMinutes = subEvent.OffsetMinutes }; 
    }//method
  }//class

  public interface ICalendarService {
    event EventHandler<CalendarEventArgs> EventFired;
    event EventHandler<CalendarEventArgs> LinkedEventFired;

  }


}
