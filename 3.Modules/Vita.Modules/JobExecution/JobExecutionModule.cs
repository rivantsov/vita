using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.Logging;
using Vita.Common;
using System.Collections.Concurrent;
using Vita.Entities.Runtime;
using Vita.Data;

namespace Vita.Modules.JobExecution {

  public partial class JobExecutionModule : EntityModule,
       IJobInformationService, IJobExecutionService, IJobDiagnosticsService  {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    JobModuleSettings _settings; 
    JsonSerializer _serializer; //used to serialize job arguments
    ITimerService _timers;
    IErrorLogService _errorLog;
    IIncidentLogService _incidentLog;
    ConcurrentDictionary<Guid, JobRunContext> _runningJobs = new ConcurrentDictionary<Guid, JobRunContext>();
    //used in pre-save validation of job definition; for light jobs (save only on error), 
    // save comes only when 1st run fails - which might not happen for a while. But when error happens, 
    // attempt to save the job might fail (name too long). To avoid this, we validate job parameters early.
    int _jobNameSize;
    ThreadSafeCounter _activitiesCounter;

    public JobExecutionModule(EntityArea area, JobModuleSettings settings = null) : base(area, "JobRunner", version: CurrentVersion) {
      RegisterEntities(typeof(IJob), typeof(IJobRun), typeof(IJobSchedule));
      _settings = settings ?? new JobModuleSettings();
      _activitiesCounter = new ThreadSafeCounter(); 
      App.RegisterConfig(_settings); 
      App.RegisterService<IJobInformationService>(this);
      App.RegisterService<IJobExecutionService>(this);
      App.RegisterService<IJobDiagnosticsService>(this); 
    } 

    public override void Init() {
      base.Init();
      _timers = App.GetService<ITimerService>();
      _errorLog = App.GetService<IErrorLogService>();
      _incidentLog = App.GetService<IIncidentLogService>();
      _serializer = new JsonSerializer();
      _jobNameSize = App.GetPropertySize<IJob>(j => j.Name); 
      //hook to events
      // every minute we check for due job runs and start them
      _timers.Elapsed1Minute += Timers_ElapsedIMinue;
      // Every 30 minutes check for long overdue jobs for ANY server
      _timers.Elapsed30Minutes += Timers_Elapsed30Minutes;
      // Whenever job run is saved, and RunType=OnSave, we start the run in this handler  
      var jobRunEntInfo = App.Model.GetEntityInfo(typeof(IJobRun));
      jobRunEntInfo.SaveEvents.SavedChanges += JobRunEntitySavedHandler;
      // On saving job schedule (CRON schedule), we automatically create the first job run
      var jobSchedEntInfo = App.Model.GetEntityInfo(typeof(IJobSchedule));
      jobSchedEntInfo.SaveEvents.SavingChanges += JobScheduleEntitySavingHandler;
    }

    private void Timers_Elapsed30Minutes(object sender, EventArgs e) {
      if(_settings.Flags.IsSet(JobModuleFlags.TakeOverLongOverdueJobs))
        CheckLongOverdueJobRuns(); 
    }

    public override void Shutdown() {
      base.Shutdown();
      SuspendJobsOnShutdown();
    }

    #region Running jobs dictionary

    private bool RegisterJobRun(JobRunContext job) {
      return _runningJobs.TryAdd(job.JobRunId, job); 
    }

    private JobRunContext GetJobRun(Guid jobRunId) {
      JobRunContext result;
      if(_runningJobs.TryGetValue(jobRunId, out result))
        return result;
      return null; 
    }
    private bool UnregisterJobRun(JobRunContext jobContext) {
      JobRunContext dummy;
      return _runningJobs.TryRemove(jobContext.JobRunId, out dummy);
    }
    #endregion

    #region Event handlers: SavedChanged, Timers_Elapsed1Minute
    private void JobRunEntitySavedHandler(EntityRecord record, EventArgs args) {
      // Start job runs that are new and that have RunType = OnSave
      var jobRun = (IJobRun)record.EntityInstance;
      if(record.StatusBeforeSave == EntityStatus.New && jobRun.RunType == JobRunType.OnSave)
        StartJobRun(jobRun);
    }

    private void JobScheduleEntitySavingHandler(Entities.Runtime.EntityRecord record, EventArgs args) {
      switch(record.Status) {
        case EntityStatus.Deleting:
          break; //nothing to do
        default:
          var sched = (IJobSchedule) record.EntityInstance;
          OnJobScheduleSaving(sched);
          break; 
      }
    }

    private void Timers_ElapsedIMinue(object sender, EventArgs e) {
      if(App.Status != EntityAppStatus.Connected)
        return;
      try {
        // we add 1 here to immediately move it from zero; for every starting job will increment it, 
        // and then decrement when it actually starts on another thread. The counter gets to zero when all 
        // activities are actually completed and due jobs actually started.
        _activitiesCounter.Increment(); 
        StartDueJobRuns();
      } finally {
        _activitiesCounter.Decrement();  
      }
    }
    #endregion

    private void SuspendJobsOnShutdown() {
      if(_runningJobs.Count == 0)
        return;
      var activeJobRunContexts = GetRunningJobs();
      foreach(var jobRunCtx in activeJobRunContexts) {
        jobRunCtx.TryCancel();
      }
      //Update statuses
      var session = App.OpenSystemSession();
      var utcNow = App.TimeService.UtcNow;
      var log = "Job stopped due to system shutdown at " + utcNow.ToLongTimeString() + Environment.NewLine;
      var jobRunIds = activeJobRunContexts.Select(j => j.JobRunId).ToArray();
      var updateQuery = session.EntitySet<IJobRun>().Where(jr => jobRunIds.Contains(jr.Id))
            .Select(jr => new { Status = JobRunStatus.Interrupted, EndedOn = utcNow, Log = jr.Log + log });
      updateQuery.ExecuteUpdate<IJobRun>();
      _runningJobs.Clear();
    }

    /// <summary>Returns contexts associated with data sources that contain tables from JobExecutionModule.</summary>
    /// <returns>A list of operation contexts.</returns>
    /// <remarks>In a multi-tenant scenario, an app may be connected to multiple databases  (data sources) with the same or different model.
    /// Each data source might have its own IJob table. When restarting/retrying jobs, we must go thru all databases.</remarks>
    private IList<OperationContext> GetContextsForConnectedDataSources() {
      var resultList = new List<OperationContext>();
      var dsList = App.DataAccess.GetDataSources();
      foreach(var ds in dsList) {
        //Check if it contains job tables
        if(!ds.ExistsTableFor(typeof(IJob)))
          continue;
        var ctx = App.CreateSystemContext();
        ctx.DataSourceName = ds.Name;
        resultList.Add(ctx);
      }
      return resultList;
    }

    #region IJobInformationService

    public IList<JobRunContext> GetRunningJobs() {
      return _runningJobs.Values.ToList();
    }
    public event EventHandler<JobNotificationEventArgs> Notify;

    private void OnJobNotify(JobRunContext jobContext, JobNotificationType notificationType, Exception exception = null) {
      Notify?.Invoke(this, new JobNotificationEventArgs() { JobRunContext = jobContext, NotificationType = notificationType, Exception = exception });
    }

    #endregion

    #region IJobDiagnosticsService members
    public void WaitStarting() {
      while(!_activitiesCounter.IsZero())
        Thread.Yield(); 
    }

    public void WaitAllCompleted() {
      while(true) {
        if(_activitiesCounter.IsZero() && _runningJobs.Count == 0)
          return;
        Thread.Yield(); 
      }
    }

    #endregion 


  }//module
}
