using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Calendar {

  public enum EventTrigger {
    LeadTime,
    Event,
  }

  public class CalendarEventArgs : EventArgs {
    public Guid Id;
    public EventTrigger Trigger; 
    public Guid CalendarId;
    public CalendarType CalendarType; 
    public string CalendarName;
    public Guid? OwnerId;
    public string Code;
    public string Title;
    public DateTime RunOn;
    public Guid? SeriesId;
    
    public CalendarEventStatus Status;
    public string ExecutionNotes;
    // free-form parameters
    public Guid? CustomItemId;
    public string CustomData; 
  }

  public interface ICalendarService {
    event EventHandler<CalendarEventArgs> EventFired;
  }

}
