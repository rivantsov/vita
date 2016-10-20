using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Modules.Calendar {
  public class CalendarEntityModule : EntityModule, ICalendarService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public CalendarUserRoles Roles; 

    //services
    ITimerService _timerService;
    IErrorLogService _errorLog; 
    DateTime _lastExecuted; 

    public CalendarEntityModule(EntityArea area) : base(area, "Calendar", version: CurrentVersion) {
      RegisterEntities(typeof(ICalendar), typeof(ICalendarEvent), typeof(ICalendarEventSeries));
      App.RegisterService<ICalendarService>(this);
      Roles = new CalendarUserRoles(); 
    }

    public override void Init() {
      base.Init();
      _errorLog = App.GetService<IErrorLogService>(); 
      _timerService = App.GetService<ITimerService>();
      _timerService.Elapsed1Minute += TimerService_Elapsed1Minute;
      var serEnt = App.Model.GetEntityInfo(typeof(ICalendarEventSeries));
      serEnt.SaveEvents.SavingChanges += EventSerier_SavingChanges;
    }

    #region event handlers
    // Checks CRON spec and sets next run time automatically when we save ICalendarEventSeries entity
    private void EventSerier_SavingChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      if(record.Status == EntityStatus.Deleting)
        return;
      var iSer = record.EntityInstance as ICalendarEventSeries;
      if(iSer.Status == CalendarEventSeriesStatus.Suspended)
        return; 
      var cron = iSer.CronSpec;
      if(!string.IsNullOrEmpty(cron))
        iSer.RecalcNextRunOn(App.TimeService.UtcNow);
    }
    private void TimerService_Elapsed1Minute(object sender, EventArgs args) {
      var utcNow = App.TimeService.UtcNow;
      var utcNowRounded = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0);
      Task.Run(() => FireEvents(utcNowRounded));
    }
    #endregion

    private void FireEvents(DateTime utcNow) {
      // If we are activated after long pause (system restart/redeployment), we need to update NextRunOn on all schedules
      // The NextRunOn might be in the past, so they will not be picked up for execution
      var needRepairSeries = (utcNow > _lastExecuted.AddMinutes(2)); 
      _lastExecuted = utcNow; 
      //Account for multiple data sources (databases), each might contain its own set of events
      var dsList = App.DataAccess.GetDataSources(); 
      foreach(var ds in dsList) {
        //Check if it contains Calendar module
        if(!ds.Database.DbModel.ContainsSchema(this.Area.Name))
          continue; 
        var ctx = App.CreateSystemContext();
        ctx.DataSourceName = ds.Name;
        // Repair if necessary
        if(needRepairSeries)
          RepairEventSeries(ctx, utcNow); 
        // First check series and create new events that are due at this time
        ProcessEventSeries(ctx, utcNow);
        // Now get all due events and fire them
        ProcessDueEvents(ctx, utcNow); 
      }//foreach ds     
    }//method

    private void RepairEventSeries(OperationContext context, DateTime utcNow) {
      var session = context.OpenSession();
      var serToRepair = session.EntitySet<ICalendarEventSeries>().Where(s => s.Status == CalendarEventSeriesStatus.Active && s.NextRunOn < utcNow).ToList();
      foreach(var ser in serToRepair)
        ser.RecalcNextRunOn(utcNow);
      session.SaveChanges(); 
    }

    private void ProcessEventSeries(OperationContext context, DateTime utcNow) {
      var session = context.OpenSession();
      var nowPlus1 = utcNow.AddMinutes(1);
      // We search schedules by NextLeadTime 
      var seriesList = session.EntitySet<ICalendarEventSeries>()
          .Where(es => es.Status == CalendarEventSeriesStatus.Active && es.NextLeadTime >= utcNow && es.NextLeadTime < nowPlus1).ToList();
      if(seriesList.Count == 0)
        return; 
      foreach(var series in seriesList) {
        //try to find already created event
        var runOn = series.NextRunOn;
        if(runOn == null)
          continue;
        var evt = session.EntitySet<ICalendarEvent>().Where(e => e.ScheduledRunOn == runOn).FirstOrDefault(); 
        if(evt == null) {
          evt = series.NewEventForSeries();
        }
      }
      session.SaveChanges(); 
    }

    private void ProcessDueEvents(OperationContext context, DateTime utcNow) {
      var session = context.OpenSession();
      // query events, find due to start now
      var nowPlus1 = utcNow.AddMinutes(1);
      //Note: we do not use condition 'evt.RunOn >= utcNow', to grab all missed passed events and execute them now
      //Lead time - fire events with lead time due
      var leadEvents = session.EntitySet<ICalendarEvent>()
        .Where(e => (e.Status == CalendarEventStatus.NotStarted) && (e.RunOn != e.LeadTime) && e.LeadTime < nowPlus1).ToList();
      foreach(var evt in leadEvents) {
        OnEventFired(context, evt, EventTrigger.LeadTime);
      }//foreach
      session.SaveChanges(); 

      //Events themselves
      var runEvents = session.EntitySet<ICalendarEvent>()
          .Where(e => (e.Status == CalendarEventStatus.NotStarted || e.Status == CalendarEventStatus.LeadFired)
            && e.RunOn < nowPlus1).ToList();
      foreach(var evt in runEvents) {
        OnEventFired(context, evt, EventTrigger.Event);
        if(evt.Series != null)
          evt.Series.LastRunOn = utcNow; //this will fire CRON scheduler and set next run on date
      }
      session.SaveChanges();
    }

    private void OnEventFired(OperationContext context, ICalendarEvent evt, EventTrigger trigger) {
      if(EventFired == null)
        return; 
      try {
        var cal = evt.Calendar;
        var args = new CalendarEventArgs() {
          Id = evt.Id, CalendarId = cal.Id, CalendarName = cal.Name, CalendarType = cal.Type, Code = evt.Code, Title = evt.Title, OwnerId = cal.OwnerId, Status = evt.Status,
          Trigger = trigger, RunOn = evt.RunOn, SeriesId = evt.Series?.Id, ExecutionNotes = evt.ExecutionNotes, CustomItemId = evt.CustomItemId, CustomData = evt.CustomData
        };
        EventFired(this, args);
        evt.ExecutionNotes = args.ExecutionNotes; 
        evt.Status = trigger == EventTrigger.LeadTime ? CalendarEventStatus.LeadFired : CalendarEventStatus.Fired;
      } catch(Exception ex) {
        evt.Status = CalendarEventStatus.Error;
        evt.ExecutionNotes = ex.ToLogString(); 
        _errorLog.LogError(ex, context); 
      }
    }

    #region ICalendarService members
    public event EventHandler<CalendarEventArgs> EventFired;

    #endregion

  }//class
}//ns
