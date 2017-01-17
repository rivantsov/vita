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

  public partial class JobExecutionModule : EntityModule, IJobInformationService, IJobExecutionService  {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    JobModuleSettings _settings; 
    JsonSerializer _serializer;
    ITimerService _timers;
    IErrorLogService _errorLog;
    IIncidentLogService _incidentLog;
    int _jobNameSize;

    public string HostName;

    public JobExecutionModule(EntityArea area, JobModuleSettings settings = null) : base(area, "JobRunner", version: CurrentVersion) {
      RegisterEntities(typeof(IJob), typeof(IJobRun), typeof(IJobSchedule));
      _settings = settings ?? new JobModuleSettings();
      App.RegisterConfig(_settings); 
      App.RegisterService<IJobInformationService>(this);
      App.RegisterService<IJobExecutionService>(this);
    } //constructor

    public override void Init() {
      base.Init();
      _timers = App.GetService<ITimerService>();
      _errorLog = App.GetService<IErrorLogService>();
      _incidentLog = App.GetService<IIncidentLogService>();
      _serializer = new JsonSerializer();
      _jobNameSize = App.GetPropertySize<IJob>(j => j.Name);
      //hook to events
      _timers.Elapsed1Minute += Timers_ElapsedIMinue;
      var jobRunEntInfo = App.Model.GetEntityInfo(typeof(IJobRun));
      jobRunEntInfo.SaveEvents.SavedChanges += JobRunEntitySavedHandler;
      var jobSchedEntInfo = App.Model.GetEntityInfo(typeof(IJobSchedule));
      jobSchedEntInfo.SaveEvents.SavedChanges += OnJobScheduleSaved;
      HostName = HostName ?? System.Net.Dns.GetHostName();
    }

    private void JobRunEntitySavedHandler(EntityRecord record, EventArgs args) {
      // we are interested only in new jobs with StartMode = OnSave
      if(record.StatusBeforeSave != EntityStatus.New)
        return;
      var jobRun = (IJobRun)record.EntityInstance;
      if(jobRun.Status == JobRunStatus.Executing)
        StartJobRun(jobRun); 
    }

    public override void Shutdown() {
      base.Shutdown();
      SuspendJobsOnShutdown();
    }

    #region Running jobs dictionary
    ConcurrentDictionary<Guid, JobRunContext> _runningJobs = new ConcurrentDictionary<Guid, JobRunContext>();

    private bool RegisterRunningJob(JobRunContext job) {
      return _runningJobs.TryAdd(job.JobId, job); 
    }

    private JobRunContext GetRunningJob(Guid jobId) {
      JobRunContext result;
      if(_runningJobs.TryGetValue(jobId, out result))
        return result;
      return null; 
    }
    private bool UnregisterRunningJob(Guid jobId) {
      JobRunContext dummy;
      return _runningJobs.TryRemove(jobId, out dummy);
    }
    #endregion

    #region Event handlers: SavedChanged, Timers_Elapsed1Minute
    private void OnJobScheduleSaved(Entities.Runtime.EntityRecord record, EventArgs args) {
    }

    // if false, we just started up
    private void Timers_ElapsedIMinue(object sender, EventArgs e) {
      if(App.Status != EntityAppStatus.Connected)
        return;
        StartDueJobRuns();
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
            .Select(jr => new { Status = JobRunStatus.Interrupted, LastEndedOn = utcNow, Log = jr.Log + log });
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

  }//module
}
