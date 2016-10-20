using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.Calendar {
  public static class CalendarExtensions {

    public static ICalendar NewCalendar(this IEntitySession session, CalendarType type, string name, Guid? ownerId = null) {
      var cal = session.NewEntity<ICalendar>();
      cal.Type = type;
      cal.Name = name;
      cal.OwnerId = ownerId;
      return cal; 
    }

    public static ICalendarEventSeries NewCalendarEventSeries(this ICalendar cal, string code, string title, string description, string cron = null) {
      var session = EntityHelper.GetSession(cal);
      var ser = session.NewEntity<ICalendarEventSeries>();
      ser.Calendar = cal;
      ser.Code = code;
      ser.Title = title;
      ser.Description = description;
      ser.CronSpec = cron;
      return ser; 
    }

    public static ICalendarEvent NewEvent(this ICalendar calendar, string code, string title, string description, DateTime runOn, 
               ICalendarEventSeries series = null, CalendarEventFlags flags = CalendarEventFlags.None, 
               Guid? customItemId = null, string customData = null) {
      var session = EntityHelper.GetSession(calendar);
      var evt = session.NewEntity<ICalendarEvent>();
      evt.Calendar = calendar;
      evt.Code = code;
      evt.Title = title;
      evt.Description = description;
      evt.ScheduledRunOn = evt.LeadTime = evt.RunOn = runOn;
      evt.Status = CalendarEventStatus.NotStarted;
      evt.Flags = flags;
      evt.Series = series;
      evt.CustomItemId = customItemId;
      evt.CustomData = customData;
      return evt; 
    }

    public static ICalendar GetDefaultUserCalendar(this IEntitySession session, Guid userId) {
      var cal = session.EntitySet<ICalendar>().Where(c => c.Type == CalendarType.Individual && c.OwnerId == userId).FirstOrDefault();
      return cal; 
    }

    public static ICalendarEvent CreateCalendarEventForUser(this IEntitySession session, Guid userId, string code, string title, string description, DateTime runOn,
                                Guid? customItemId = null, string customData = null, CalendarEventFlags flags = CalendarEventFlags.None) {
      var cal = session.GetDefaultUserCalendar(userId);
      if(cal == null)
        cal = session.NewCalendar(CalendarType.Individual, "UserCalendar", userId);
      var evt = session.NewEntity<ICalendarEvent>();
      evt.Calendar = cal;
      evt.Code = code;
      evt.Title = title;
      evt.Description = description;
      evt.ScheduledRunOn = evt.LeadTime = evt.RunOn = runOn;
      evt.Status = CalendarEventStatus.NotStarted;
      evt.CustomItemId = customItemId;
      evt.CustomData = customData;
      evt.Flags = flags;
      return evt;
    }

    public static ICalendarEvent NewEventForSeries(this ICalendarEventSeries series, CalendarEventFlags flags = CalendarEventFlags.None) {
      Util.Check(series.NextRunOn != null, "CalendarSeries.NextRunOn may not be null, cannot create new event.");
      var session = EntityHelper.GetSession(series);
      var evt = session.NewEntity<ICalendarEvent>();
      evt.Series = series;
      evt.Calendar = series.Calendar;
      evt.Code = series.Code;
      evt.Title = series.Title;
      evt.Description = series.Description;
      evt.ScheduledRunOn = series.NextRunOn;
      evt.RunOn = series.NextRunOn.Value;
      evt.LeadTime = series.NextLeadTime == null ? evt.RunOn : series.NextLeadTime.Value;
      evt.Status = CalendarEventStatus.NotStarted;
      evt.Flags = flags;
      return evt;
    }

    public static void RecalcNextRunOn(this ICalendarEventSeries series, DateTime utcNow) {
      var cron = series.CronSpec;
      if(string.IsNullOrWhiteSpace(cron))
        return; 
      var cronSched = new Cron.CronSchedule(cron);
      var start = series.LastRunOn == null ? utcNow : series.LastRunOn.Value;
      var nextRunOn = cronSched.TryGetNext(start);
      // If it comes in the past, schedule again from current date
      if(nextRunOn != null && nextRunOn.Value < utcNow)
        nextRunOn = cronSched.TryGetNext(utcNow);
      series.SetNextRunOn(nextRunOn); 
    }

    public static void SetNextRunOn(this ICalendarEventSeries series, DateTime? nextRunOn) {
      if(nextRunOn == null) {
        //no more runs
        series.NextRunOn = null;
        series.NextLeadTime = null;
      } else {
        series.NextRunOn = nextRunOn;
        series.NextLeadTime = nextRunOn.Value.AddMinutes(-series.LeadInterval);
      }
    }
  }
}
