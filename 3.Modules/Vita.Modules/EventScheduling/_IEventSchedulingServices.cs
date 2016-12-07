using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.EventScheduling {

  public class SchedulingEventArgs : EventArgs {
    public Guid Id;
    public Guid InfoId;
    public Guid? OwnerId;
    public string Code;
    public string Title;
    public DateTime StartOn;
    
    public EventStatus Status;
    public string Log;
    // free-form parameters
    public Guid? DataId;
    public string Data;

    public SchedulingEventArgs(IEvent evt) {
      Id = evt.Id;
      Code = evt.Code;
      var info = evt.EventInfo;
      InfoId = info.Id; 
      Title = info.Title;
      OwnerId = evt.EventInfo.OwnerId;
      Status = evt.Status;
      StartOn = evt.StartOn;
      DataId = info.DataId;
      Data = info.Data;

    }//method
  }//class

  public class SchedulingErrorEventArgs : SchedulingEventArgs {
    public Exception Exception; 
    public SchedulingErrorEventArgs(IEvent evt, Exception exception) : base(evt) {
      Exception = exception; 
    }
  }

  public interface IEventSchedulingService {
    event EventHandler<SchedulingEventArgs> EventFired;
    event EventHandler<SchedulingErrorEventArgs> EventFailed;
  }


}
