using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.EventScheduling {
  // IEventSchedulingService implementation

  public partial class EventSchedulingModule {

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
    public event EventHandler<SchedulingEventArgs> EventFired;
    public event EventHandler<SchedulingErrorEventArgs> EventFailed;
    #endregion


    private void ProcessAndFireEvents(DateRange range) {
      var checkMissedSchedules = NeedToCheckMissedSchedules(range);
      _lastExecutedOn = range.UtcNow;
      //Account for multiple data sources (databases), each might contain its own set of events
      var dsList = App.DataAccess.GetDataSources();
      foreach(var ds in dsList) {
        //Check if it contains Calendar module
        if(!ds.Database.DbModel.ContainsSchema(this.Area.Name))
          continue;
        var ctx = App.CreateSystemContext();
        ctx.DataSourceName = ds.Name;
        FireEventsForContext(ctx, range, checkMissedSchedules);
      }//foreach ds  
    }//method

    private void FireEventsForContext(OperationContext ctx, DateRange range, bool checkMissedSchedules) {
      try {
        var session = ctx.OpenSession();
        // if we restart after pause, process missed scheduled events
        if(checkMissedSchedules)
          ProcessMissedSchedules(session, range);
        // First check series and create new events that are due at this time
        ProcessComingScheduledEvents(session, range);
        // Now get all due events and fire them
        ProcessDueEvents(session, range);
      } catch(Exception ex) {
        _errorLog.LogError(ex, ctx);
      }
    }

    private bool NeedToCheckMissedSchedules(DateRange range) {
      // if lastExecuted is not set (it is restart), or last executed was more than 5 minutes ago (for whatever reason) - check missed schedules
      if(_lastExecutedOn == null || _lastExecutedOn < range.UtcNow.AddMinutes(-5))
        return true;
      return false; 
    }

    //TO complete
    private void ProcessMissedSchedules(IEntitySession session, DateRange range) {
      var schedules = session.EntitySet<IEventSchedule>()
        .Where(s => s.Status == ScheduleStatus.Active && s.NextStartOn < range.From).ToList();
      foreach(var sched in schedules) {
        //create event to execute now; calc next startOn
        var evt = sched.NewScheduledEvent(range.UtcNow);
        sched.LastStartedOn = evt.StartOn; 
      }
      session.SaveChanges();
    }

    private void ProcessComingScheduledEvents(IEntitySession session, DateRange range) {
      int BatchSize = 100;
      while(true) {
        //Find and process due schedules in batches of 100 
        var schedules = session.EntitySet<IEventSchedule>()
            .Where(es => es.Status == ScheduleStatus.Active &&
                // we are picking up all past events that are still active
                es.NextStartOn != null && es.NextStartOn < range.Until)
                .Take(BatchSize)
                .ToList();
        if(schedules.Count == 0)
          return;
        foreach(var sched in schedules) {
          var evt = sched.NewScheduledEvent(sched.NextStartOn.Value);
          sched.LastStartedOn = evt.StartOn; // on sched save next start will be recalculated
        }
        session.SaveChanges();
        if(schedules.Count < BatchSize)
          return; //there are no more
      }//while
    }

    private void ProcessDueEvents(IEntitySession session, DateRange range) {
      int BatchSize = 100;
      while(true) {
        // Find and process due instances in batches of 100
        var dueEvents = session.EntitySet<IEvent>()
                              .Include(e => e.EventInfo)
                              .Where(e => (e.StartOn >= range.From && e.StartOn < range.Until))
                              .Take(BatchSize)
                              .ToList();
        if(dueEvents.Count == 0)
          return;
        foreach(var evt in dueEvents)
          OnEventFired(session.Context, evt);
        session.SaveChanges();
        if(dueEvents.Count < BatchSize)
          return; //there are no more
      }
    }

    private void OnEventFired(OperationContext context, IEvent evt) {
      try {
        var evtFired = EventFired;
        if(evtFired != null) {
          var args = new SchedulingEventArgs(evt);
          evtFired(this, args);
          evt.Log = args.Log;
          evt.Status = args.Status;
        }
        var job = evt.EventInfo.JobToRun;
        if(job != null)
          _jobService.StartJob(context, job.Id, evt.Id);
        if(evt.Status == EventStatus.Pending) //if event handler did not set it
          evt.Status = EventStatus.Completed;
      } catch(Exception ex) {
        evt.Status = EventStatus.Error;
        evt.Log = ex.ToLogString();
        _errorLog.LogError(ex, context);
        OnEventFailed(evt, ex);
      }
    }

    private void OnEventFailed(IEvent evt, Exception exc) {
      EventFailed?.Invoke(this, new SchedulingErrorEventArgs(evt, exc));
    }


  }//class
} //ns
