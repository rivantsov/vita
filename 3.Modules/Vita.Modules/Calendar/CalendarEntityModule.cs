using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Modules.JobExecution;

namespace Vita.Modules.Calendar {
  public partial class CalendarEntityModule : EntityModule, ICalendarService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public CalendarUserRoles Roles { get; private set; } 

    //services
    ITimerService _timerService;
    IJobExecutionService _jobService; 
    IErrorLogService _errorLog; 
    DateTime? _lastExecutedOn; // if null, it is first after restart

    public CalendarEntityModule(EntityArea area) : base(area, "CalendarModule", version: CurrentVersion) {
      Requires<JobExecution.JobExecutionModule>();
      RegisterEntities(typeof(IEventCalendar), typeof(IEvent), 
            typeof(IEventTemplate), typeof(IEventSubEvent), typeof(IEventSchedule));
      App.RegisterService<ICalendarService>(this);
      Roles = new CalendarUserRoles(); 
    }

    public override void Init() {
      base.Init();
      _errorLog = App.GetService<IErrorLogService>(); 
      _timerService = App.GetService<ITimerService>();
      _timerService.Elapsed1Minute += TimerService_Elapsed1Minute;
      _jobService = App.GetService<IJobExecutionService>(); 
      // signup to all 3 events
      var ent = App.Model.GetEntityInfo(typeof(IEventTemplate));
      ent.SaveEvents.SavingChanges += EventTemplates_SavingChanges;
      ent = App.Model.GetEntityInfo(typeof(IEventSubEvent));
      ent.SaveEvents.SavingChanges += EventTemplates_SavingChanges;
    }

    #region event handlers
    private void EventTemplates_SavingChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      var entType = record.EntityInfo.EntityType;
      if (entType == typeof(IEventTemplate)) {
        var templ = (IEventTemplate)record.EntityInstance;
        AddModifiedTemplateId(templ.Id);
      } else if (entType == typeof(IEventSubEvent)) {
        var templ = (IEventSubEvent)record.EntityInstance;
        AddModifiedTemplateId(templ.Event.Id); 
      }
    }

    private void TimerService_Elapsed1Minute(object sender, EventArgs args) {
      var utcNow = App.TimeService.UtcNow;
      if(_lastExecutedOn == null)
        OnRestart(utcNow); 
      var dateRange = new DateRange(utcNow);
      // Fire events asynchronously
      Task.Run(() => ProcessAndFireEvents(dateRange));
    }
    #endregion

    #region Modified templates 
    HashSet<Guid> _modifiedTemplateIds = new HashSet<Guid>();
    object _lock = new object(); 
    private IList<Guid> GetModifiedTemplateIds() {
      lock(_lock) {
        var result = _modifiedTemplateIds.ToList();
        _modifiedTemplateIds.Clear();
        return result; 
      }
    }
    private void AddModifiedTemplateId(Guid id) {
      lock(_lock) {
        _modifiedTemplateIds.Add(id); 
      }
    }
    #endregion 
  }//class
}//ns
