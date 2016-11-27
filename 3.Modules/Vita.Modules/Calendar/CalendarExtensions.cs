using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Modules.JobExecution;

namespace Vita.Modules.Calendar {

  // We make the class public, to ensure all operations go thru calendar service
  public static class CalendarExtensions {

    public static bool IsSet(this EventFlags flags, EventFlags flag) {
      return (flags & flag) != 0; 
    }

    public static IEventCalendar FindCalendar(this IEntitySession session, CalendarType type, Guid ownerId) {
      var cal = session.EntitySet<IEventCalendar>()
          .Where(c => c.Type == type && c.OwnerId == ownerId).FirstOrDefault();
      return cal;
    }

    public static IEventCalendar NewCalendar(this IEntitySession session, CalendarType type, string name, Guid? ownerId = null) {
      var cal = session.NewEntity<IEventCalendar>();
      cal.Type = type;
      cal.Name = name;
      cal.OwnerId = ownerId;
      return cal; 
    }

    public static IEventTemplate NewEventTemplate(this IEventCalendar calendar, 
               string code, string title, string description, EventFlags flags = EventFlags.None,  
               Guid? customId = null, string customData = null, IJob jobToRun = null) {
      var session = EntityHelper.GetSession(calendar);
      var template = session.NewEntity<IEventTemplate>();
      template.Calendar = calendar; 
      template.Code = code;
      template.Title = title;
      template.Description = description;
      template.Flags = flags;
      template.CustomId = customId;
      template.CustomData = customData;
      template.JobToRun = jobToRun; 
      return template; 
    }

    public static IEventSubEvent AddSubEvent(this IEventTemplate template, string code, string title, 
                                                         int offsetMinutes, IJob jobToRun = null) {
      var session = EntityHelper.GetSession(template);
      var le = session.NewEntity<IEventSubEvent>();
      le.Event = template;
      le.Code = code;
      le.Title = title; 
      le.OffsetMinutes = offsetMinutes;
      le.JobToRun = jobToRun;
      template.SubEvents.Add(le);
      return le; 
    }

    public static IEventSchedule NewSchedule(this IEventCalendar calendar, IEventTemplate eventTemplate, 
              string cronSpec, DateTime? activeFrom = null, DateTime? activeUntil = null) {
      var session = EntityHelper.GetSession(calendar);
      var utcNow = session.Context.App.TimeService.UtcNow;
      var sched = session.NewEntity<IEventSchedule>();
      sched.Calendar = calendar;
      sched.Template = eventTemplate;
      sched.Status = ScheduleStatus.Active;
      sched.ActiveFrom = activeFrom != null ? activeFrom.Value : utcNow;
      sched.ActiveUntil = activeUntil;
      sched.RecalcNextStartOn(utcNow);
      return sched; 
    }

    public static IEvent NewEvent(this IEventCalendar calendar, IEventTemplate template, DateTime startOn) {
      var session = EntityHelper.GetSession(calendar);
      var evt = session.NewEntity<IEvent>();
      evt.Calendar = calendar;
      evt.Template = template;
      evt.StartOn = startOn;
      var leadTime = template.GetFirstSubEventLeadTime(); //zero or negative
      var nextActiveOn = startOn.AddMinutes(leadTime);
      var utcNow = session.Context.App.TimeService.UtcNow;
      if(nextActiveOn > utcNow)
        nextActiveOn = utcNow;
      return evt; 
    }

    public static IEvent NewScheduledEvent(this IEventSchedule schedule, DateTime startOn, EventFlags flags = EventFlags.None) {
      Util.Check(schedule.NextStartOn != null, "CalendarSeries.NextRunOn may not be null, cannot create new event.");
      var session = EntityHelper.GetSession(schedule);
      var evt = session.NewEntity<IEvent>();
      evt.Calendar = schedule.Calendar; 
      evt.Template = schedule.Template;
      evt.StartOn = evt.OriginalStartOn = startOn;
      evt.NextActivateOn = schedule.NextActivateOn;
      evt.Status = EventStatus.Pending;
      evt.NextActivateOn = schedule.GetFirstActivateOn();
      return evt;
    }

    public static DateTime? GetFirstActivateOn(this IEventSchedule schedule) {
      var nextStart = schedule.NextStartOn;
      if(nextStart == null)
        return null; 
      var firstLinked = schedule.Template.SubEvents.OrderBy(le => le.OffsetMinutes).FirstOrDefault();
      if(firstLinked == null)
        return nextStart;
      return nextStart.Value.AddMinutes(firstLinked.OffsetMinutes);
    }

    public static int GetFirstSubEventLeadTime(this IEventTemplate template) {
      var firstLinked = template.SubEvents.OrderBy(le => le.OffsetMinutes).FirstOrDefault();
      if(firstLinked == null || firstLinked.OffsetMinutes > 0)
        return 0; //no lead time
      return firstLinked.OffsetMinutes; 

    }

    public static void UpdateNextActivateOnAfter(this IEvent inst, DateTime afterTime) {
      var nextLinked = inst.Template.SubEvents.OrderBy(le => le.OffsetMinutes).FirstOrDefault(le => inst.StartOn.AddMinutes(le.OffsetMinutes) > afterTime);
      if(nextLinked == null) {
        if (inst.NextActivateOn < afterTime)
          inst.Status = EventStatus.Completed;
        inst.NextActivateOn = null;
      } else {
        inst.Status = EventStatus.Firing;
        inst.NextActivateOn = inst.StartOn.AddMinutes(nextLinked.OffsetMinutes);
      }
    }


    public static void RecalcNextStartOn(this IEventSchedule schedule, DateTime utcNow) {
      if (schedule.Status != ScheduleStatus.Active) {
        schedule.NextStartOn = null;
        schedule.NextActivateOn = null;
        return; 
      }
      var cron = schedule.ScheduleCronSpec;
      if(string.IsNullOrWhiteSpace(cron))
        return; 
      var cronSched = new Cron.CronSchedule(cron);
      var start = utcNow; // evt.LastRunOn == null ? utcNow : evt.LastRunOn.Value;
      var nextRunOn = cronSched.TryGetNext(start);
      // If it comes in the past, schedule again from current date
      if(nextRunOn != null && nextRunOn.Value < utcNow)
        nextRunOn = cronSched.TryGetNext(utcNow);
      schedule.NextStartOn = nextRunOn;
      schedule.NextActivateOn = schedule.GetFirstActivateOn();
    }

  }
}
