using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 
using Vita.Entities.Services;
using Vita.Modules.Logging.Api;

namespace Vita.Modules.Logging {

  public class EventLogModule : EntityModule, IEventLogService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    IBackgroundSaveService _saveService; 

    public EventLogModule(EntityArea area) : base(area, "EventLog", version: CurrentVersion) {
      this.RegisterEntities(typeof(IEvent), typeof(IEventParameter));
      App.RegisterService<IEventLogService>(this); 
    }

    public override void Init() {
      base.Init();
      _saveService = App.GetService<IBackgroundSaveService>(); 
    }

    //IEventLog service implementation
    public void LogEvents(IList<EventData> events) {
      _saveService.AddObject(new EventsPack() { Events = events });
    }

    #region nested class EventsPack
    class EventsPack : IObjectSaveHandler {
      public IList<EventData> Events;

      public void SaveObjects(IEntitySession session, IList<object> items) {
        foreach (EventsPack pack in items) {
          foreach (var evt in pack.Events) {
            var ev = session.NewEvent(evt); 
          }
        }//foreach
      }//method

    }//class
    #endregion

  }//class
}
