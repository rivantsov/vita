using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Modules.JobExecution;

namespace Vita.Modules.EventScheduling {
  public partial class EventSchedulingModule : EntityModule, IEventSchedulingService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    //services
    ITimerService _timerService;
    IJobExecutionService _jobService; 
    IErrorLogService _errorLog; 
    DateTime? _lastExecutedOn; // if null, it is first after restart

    public EventSchedulingModule(EntityArea area) : base(area, "EventSchedulingModule", version: CurrentVersion) {
      Requires<JobExecution.JobExecutionModule>();
      RegisterEntities(typeof(IEvent), typeof(IEventInfo), typeof(IEventSchedule));
      App.RegisterService<IEventSchedulingService>(this);
    }

    public override void Init() {
      base.Init();
      _errorLog = App.GetService<IErrorLogService>(); 
      _timerService = App.GetService<ITimerService>();
      _timerService.Elapsed1Minute += TimerService_Elapsed1Minute;
      _jobService = App.GetService<IJobExecutionService>(); 
      // signup to all 3 events
      var ent = App.Model.GetEntityInfo(typeof(IEventSchedule));
      ent.SaveEvents.SavingChanges += EventSchedule_SavingChanges;
    }

    #region event handlers
    private void EventSchedule_SavingChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      var session = record.Session;
      switch(record.Status) {
        case EntityStatus.New:
        case EntityStatus.Modified: break;
        default: return; 
      }
      IEventSchedule sched = (IEventSchedule)record.EntityInstance;
      sched.RecalcNextStartOn();
    }

    private void TimerService_Elapsed1Minute(object sender, EventArgs args) {
      var utcNow = App.TimeService.UtcNow;
      var dateRange = new DateRange(utcNow);
      //Start asynchonously, to avoid delaying timer event
      var thread = new Thread(ThreadJobProcessEvents);
      thread.Start(dateRange);
    }

    private void ThreadJobProcessEvents(object data) {
      try {
        var range = (DateRange)data;
        ProcessAndFireEvents(range);
      } catch(Exception ex) {
        _errorLog.LogError(ex); 
      }
    }
    #endregion

  }//class
}//ns
