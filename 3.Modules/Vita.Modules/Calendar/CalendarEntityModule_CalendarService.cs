using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.Calendar {
  // ICalendarService implementation
  public partial class CalendarEntityModule {

    class DateRange {
      public DateTime UtcNow;
      public DateTime From;
      public DateTime Until;
      public DateRange(DateTime utcNow) {
        UtcNow = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0);
        From = UtcNow.AddSeconds(-10);
        Until = UtcNow.AddMinutes(1);
      }
      public bool InRange(DateTime value) {
        return value > From && value <= Until;
      }
    }

    #region ICalendarService members
    public event EventHandler<CalendarEventArgs> EventFired;
    public event EventHandler<CalendarEventArgs> LinkedEventFired;
    #endregion


    private void OnRestart(DateTime utcNow) {

    }

    private void ProcessAndFireEvents(DateRange range) {
      // get modified templates - we'll need to update schedules (nextActivateOn) that use these templates
      var modifiedTemplateIds = GetModifiedTemplateIds(); 
      //Account for multiple data sources (databases), each might contain its own set of events
      var dsList = App.DataAccess.GetDataSources();
      foreach(var ds in dsList) {
        //Check if it contains Calendar module
        if(!ds.Database.DbModel.ContainsSchema(this.Area.Name))
          continue;
        var ctx = App.CreateSystemContext();
        ctx.DataSourceName = ds.Name;
        FireEventsForContext(ctx, range, modifiedTemplateIds);
      }//foreach ds  
      _lastExecutedOn = range.UtcNow;
    }//method

    private void FireEventsForContext(OperationContext ctx, DateRange range, IList<Guid> modifiedTemplateIds) {
      var session = ctx.OpenSession();
      // if we restart after pause, process missed scheduled events
      if(_lastExecutedOn == null)
        ProcessMissedScheduleEvents(session, range.UtcNow);
      if(modifiedTemplateIds.Count > 0)
        UpdateSchedulesThatUseTemplates(session, range.UtcNow, modifiedTemplateIds);
      // First check series and create new events that are due at this time
      ProcessComingScheduledEvents(session, range);
      // Now get all due events and fire them
      ProcessDueEvents(session, range);
    }

    private void UpdateSchedulesThatUseTemplates(IEntitySession session, DateTime utcNow, IList<Guid> templateIds) {
      var schedules = session.EntitySet<IEventSchedule>()
        .Include(s => s.Template)
        .Include((IEventTemplate t) => t.SubEvents)
        .Where(s => s.Status == ScheduleStatus.Active && templateIds.Contains(s.Template.Id))
        .ToList();
      if(schedules.Count == 0)
        return;
      foreach(var sched in schedules)
        sched.RecalcNextStartOn(utcNow);
      session.SaveChanges();       
    }

    //TO complete
    private void ProcessMissedScheduleEvents(IEntitySession session, DateTime utcNow) {
      /*
      var events = session.EntitySet<IEventTemplate>()
        .Where(s => s.ScheduleStatus == EventScheduleStatus.Active && s.NextStartOn < utcNow).ToList();
      foreach(var evt in events)
        evt.RecalcNextStartOn(utcNow);
      session.SaveChanges();
      */
    }

    private void ProcessComingScheduledEvents(IEntitySession session, DateRange range) {
      int BatchSize = 100;
      while(true) {
        // We search events with schedules
        var schedules = session.EntitySet<IEventSchedule>()
            .Where(es => es.Status == ScheduleStatus.Active &&
                es.NextStartOn != null && es.NextActivateOn >= range.From && es.NextActivateOn < range.Until)
                .Take(BatchSize)
                .ToList();
        if(schedules.Count == 0)
          return;
        ProcessComingScheduledEventsBatch(session, schedules, range);
        session.SaveChanges();
      }
    }

    private void ProcessComingScheduledEventsBatch(IEntitySession session, IList<IEventSchedule> schedules, DateRange range) {
      // When we create event instances from schedules, we must take into account that 
      //   a schedule might have an already created instance (customized version) in the current time interval. 
      // So we preload such possible instances here, to check against this list
      var eventIds = schedules.Select(e => e.Id).ToList();
      var existingEvents = session.EntitySet<IEvent>().Include(ei => ei.Template)
        .Where(ei => ei.OriginalStartOn > range.From && ei.OriginalStartOn < range.Until && eventIds.Contains(ei.Template.Id))
        .ToList();
      // Go through schedules and create events
      foreach(var sched in schedules) {
        //try to find already created event
        var existingEvt = existingEvents.FirstOrDefault(evt => evt.Template == sched && evt.OriginalStartOn == sched.NextStartOn );
        if(existingEvt == null) {
          var newInst = sched.NewScheduledEvent(sched.NextStartOn.Value); //it is not null for sure
        }
        //Update schedule's next run
        sched.RecalcNextStartOn(range.UtcNow);
      }
    }

    private void ProcessDueEvents(IEntitySession session, DateRange range) {
      int BatchSize = 100;
      while(true) {
        // Find and process due instances in batches of 100
        var dueInstances = session.EntitySet<IEvent>()
                              .Include(ei => ei.Template)
                              .Include((IEventTemplate ei) => ei.SubEvents)
                              .Where(ei => (ei.NextActivateOn > range.From && ei.NextActivateOn < range.Until))
                              .Take(BatchSize)
                              .ToList();
        if(dueInstances.Count == 0)
          return;
        ProcessDueEventsBatch(session.Context, dueInstances, range);
        session.SaveChanges();
      }
    }

    private void ProcessDueEventsBatch(OperationContext context, IList<IEvent> dueInstances, DateRange range) {
      foreach(var inst in dueInstances) {
        if(range.InRange(inst.StartOn))
          OnEventFired(context, inst);
        foreach(var linkedEvt in inst.Template.SubEvents) {
          var startOn = inst.StartOn.AddMinutes(linkedEvt.OffsetMinutes);
          if(range.InRange(startOn)) {
            OnLinkedEventFired(context, inst, linkedEvt);
          }
        }
        inst.UpdateNextActivateOnAfter(range.Until);
        // update next run on 
      }//foreach inst
    }


    private void OnEventFired(OperationContext context, IEvent evt) {
      try {
        var evtFired = EventFired;
        if(evtFired != null) {
          var args = new CalendarEventArgs(evt);
          evtFired(this, args);
          evt.Log = args.Log;
          evt.Status = args.Status;
        }
        var job = evt.Template.JobToRun;
        if(job != null)
          _jobService.StartJob(context, job.Id, evt.Id);
        if(evt.Status == EventStatus.Pending) //if event handler did not set it
          evt.Status = EventStatus.Completed;
      } catch(Exception ex) {
        evt.Status = EventStatus.Error;
        evt.Log = ex.ToLogString();
        _errorLog.LogError(ex, context);
      }
    }

    private void OnLinkedEventFired(OperationContext context, IEvent evt, IEventSubEvent subEvt) {
      try {
        var evtFired = LinkedEventFired;
        if(evtFired != null) {
          var args = new CalendarEventArgs(evt, subEvt);
          evtFired(this, args);
          evt.Log = args.Log;
          evt.Status = args.Status;
        }
        var job = subEvt.JobToRun;
        if(job != null)
          _jobService.StartJob(context, job.Id, evt.Id);

      } catch(Exception ex) {
        evt.Status = EventStatus.Error;
        evt.Log += Environment.NewLine + ex.ToLogString();
        _errorLog.LogError(ex, context);
      }
    }


  }//class
} //ns
