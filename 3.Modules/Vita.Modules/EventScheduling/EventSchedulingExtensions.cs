using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.EventScheduling {

  // We make the class public, to ensure all operations go thru calendar service
  public static class EventSchedulingExtensions {

    public static bool IsSet(this EventFlags flags, EventFlags flag) {
      return (flags & flag) != 0; 
    }

    public static IEvent CreateEvent(this IEventInfo eventInfo, DateTime startOn) {
      var session = EntityHelper.GetSession(eventInfo);
      var evt = session.NewEntity<IEvent>();
      evt.EventInfo = eventInfo;
      evt.Code = eventInfo.Code;
      evt.StartOn = startOn;
      evt.Status = EventStatus.Pending;
      return evt; 
    }

    public static IEvent CreateEvent(this IEntitySession session, DateTime startOn,
               string code, string title, string description, EventFlags flags = EventFlags.None, IJob jobToRun = null,
               Guid? ownerId = null, Guid? dataId = null, string data = null) {
      var evtInfo = session.NewEventInfo(code, title, description, flags, jobToRun, ownerId, dataId, data);
      return evtInfo.CreateEvent(startOn); 
    }

    public static IEvent CreateEvent(this IEntitySession session, DateTime startOn,
               string code, string title, string description, Expression<Func<JobRunContext, Task>> asyncJobTask, EventFlags flags = EventFlags.None,
               Guid? ownerId = null, Guid? dataId = null, string data = null) {
      var jobService = session.Context.App.GetService<IJobExecutionService>();
      var job = jobService.CreatePoolJob(session, code, asyncJobTask, JobFlags.None);
      return session.CreateEvent(startOn, code, title, description, flags, job, ownerId, dataId, data);
    }

    public static IEvent CreateEvent(this IEntitySession session, DateTime startOn,
               string code, string title, string description, Expression<Action<JobRunContext>> backgroundJob, EventFlags flags = EventFlags.None,
               Guid? ownerId = null, Guid? dataId = null, string data = null) {
      var jobService = session.Context.App.GetService<IJobExecutionService>();
      var job = jobService.CreateBackgroundJob(session, code, backgroundJob, JobFlags.PersistArguments);
      return session.CreateEvent(startOn, code, title, description, flags, job, ownerId, dataId, data);
    }

    public static IEventInfo NewEventInfo(this IEntitySession session, 
               string code, string title, string description, EventFlags flags = EventFlags.None, IJob jobToRun = null, 
               Guid? ownerId = null,  Guid? dataId = null, string data = null) {
      var evt = session.NewEntity<IEventInfo>();
      evt.Code = code;
      evt.Title = title;
      evt.Description = description;
      evt.Flags = flags;
      evt.JobToRun = jobToRun;
      evt.OwnerId = ownerId;
      evt.DataId = dataId;
      evt.Data = data;
      return evt; 
    }

    public static IEventSchedule CreateSchedule(this IEventInfo evt, string cronSpec, DateTime? activeFrom = null, DateTime? activeUntil = null) {
      var session = EntityHelper.GetSession(evt);
      var utcNow = session.Context.App.TimeService.UtcNow;
      var sched = session.NewEntity<IEventSchedule>();
      sched.EventInfo = evt;
      sched.Status = ScheduleStatus.Active;
      sched.CronSpec = cronSpec; 
      sched.ActiveFrom = activeFrom != null ? activeFrom.Value : utcNow;
      sched.ActiveUntil = activeUntil;
      return sched; 
    }

    public static IEvent NewScheduledEvent(this IEventSchedule schedule, DateTime startOn) {
      Util.Check(schedule.NextStartOn != null, "CalendarSeries.NextRunOn may not be null, cannot create new event.");
      var session = EntityHelper.GetSession(schedule);
      var evt = session.NewEntity<IEvent>();
      evt.EventInfo = schedule.EventInfo;
      evt.Code = evt.EventInfo.Code;
      evt.StartOn = startOn;
      evt.Status = EventStatus.Pending;
      return evt;
    }

    public static void RecalcNextStartOn(this IEventSchedule schedule) {
      schedule.NextStartOn = null;
      if(schedule.Status != ScheduleStatus.Active) 
        return; 
      try {
        var cron = schedule.CronSpec;
        if(string.IsNullOrWhiteSpace(cron))
          return;
        //figure out start date for calculation
        var session = EntityHelper.GetSession(schedule);
        var utcNow = session.Context.App.TimeService.UtcNow;
        var lastStart = schedule.LastStartedOn;
        var fromDate = lastStart == null ? utcNow : (lastStart.Value > utcNow ? lastStart.Value : utcNow);
        if(fromDate < schedule.ActiveFrom)
          fromDate = schedule.ActiveFrom;
        if(schedule.ActiveUntil != null && fromDate > schedule.ActiveUntil.Value)
          return;
        //Create CRON engine and calc next run
        var cronSched = new Cron.CronSchedule(cron);
        var nextRunOn = cronSched.TryGetNext(fromDate);
        // If it comes after scheduled.ActiveUntil, 
        if(nextRunOn != null && schedule.ActiveUntil != null && nextRunOn > schedule.ActiveUntil.Value)
          nextRunOn = null;
        schedule.NextStartOn = nextRunOn;
      } finally {
        if(schedule.NextStartOn == null)
          schedule.Status = ScheduleStatus.Stopped;
      }
    }

  }
}
