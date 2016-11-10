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

namespace Vita.Modules.JobExecution {

  public partial class JobExecutionModule : EntityModule, IJobExecutionService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    JsonSerializer _serializer;
    ITimerService _timers;
    IErrorLogService _errorLog;
    IIncidentLogService _incidentLog;

    public JobExecutionModule(EntityArea area) : base(area, "JobRunner", version: CurrentVersion) {
      RegisterEntities(typeof(IJob), typeof(IJobRun));
      App.RegisterService<IJobExecutionService>(this); 
    } //constructor

    public override void Init() {
      base.Init();
      _timers = App.GetService<ITimerService>();
      _errorLog = App.GetService<IErrorLogService>();
      _incidentLog = App.GetService<IIncidentLogService>();
      _serializer = new JsonSerializer();
      //hook to events
      _timers.Elapsed10Seconds += Timers_Elapsed10Seconds;
      var ent = App.Model.GetEntityInfo(typeof(IJob));
      ent.SaveEvents.SavedChanges += SaveEvents_SavedChanges;
      ent.SaveEvents.SavingChanges += SaveEvents_SavingChanges;
    }

    public override void Shutdown() {
      base.Shutdown();
      ShutdownJobs();
      Thread.Sleep(50); //let jobs shutdown gracefully
    }

    #region IJobExecutionService members

    public async Task<JobRunContext> RunLightTaskAsync(OperationContext context, Expression<Func<JobRunContext, Task>> func, 
                     string jobCode, Guid? sourceId = null, JobRetryPolicy retryPolicy = null) {
      JobRunContext jobCtx = null;
      try {
        jobCtx = new JobRunContext(this.App, _serializer, jobCode, sourceId);
        RegisterRunningJob(jobCtx);
        OnJobNotify(jobCtx, JobNotificationType.Starting);
        var compiledFunc = func.Compile();
        jobCtx.Task = Task.Run(() => (Task)compiledFunc.Invoke(jobCtx), jobCtx.CancellationToken);
        await jobCtx.Task;
        UnregisterRunningJob(jobCtx.JobId);
        jobCtx.Status = JobRunStatus.Completed;
        OnJobNotify(jobCtx, JobNotificationType.Completed);
        return jobCtx;
      } catch(Exception ex) {
        if(jobCtx == null)
          throw new Exception("Failed to create JobRunContext for light task: " + ex.Message, ex);
        //Failure is ok for now, it is expected eventually; just save the job for retries
        SaveFailedLightTask(context, jobCtx, func, jobCode, sourceId, ex, retryPolicy ?? JobRetryPolicy.DefaultLightTask);
        return jobCtx;
      }
    }

    public IJob CreateJob(IEntitySession session, JobDefinition job) {
      var ent = session.NewJob(job, _serializer);
      return ent;
    }

    public IJob CreateBackgroundJob(IEntitySession session, string code, Expression<Action<JobRunContext>> expression, JobFlags flags = JobFlags.Default, 
                                     JobRetryPolicy retryPolicy = null, IJob parentJob = null) {
      var jobDef = JobDefinition.CreateBackgroundJob(code, expression, flags, retryPolicy);
      var job = JobUtil.NewJob(session, jobDef, _serializer);
      job.ParentJob = parentJob;
      ValidateNewJob(job);
      return job; 
    }

    public IJob CreatePoolJob(IEntitySession session, string code, Expression<Func<JobRunContext, Task>> expression, JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null,
              IJob parentJob = null) {
      var jobDef = JobDefinition.CreatePoolJob(code, expression, flags, retryPolicy);
      var job = JobUtil.NewJob(session, jobDef, _serializer);
      job.ParentJob = parentJob;
      ValidateNewJob(job);
      return job;
    }

    private void ValidateNewJob(IJob job) {
      if(job.ParentJob != null) {
        job.Flags |= JobFlags.HasChildJobs;
        Util.Check(!job.Flags.IsSet(JobFlags.StartOnSave),
              "Invalid job definition: the flag StartOnSave may not be set on a job with a parent job. Job code: {0}", job.Code);
      }
    } //method



    public void StartJob(OperationContext context, Guid jobId, Guid? sourceId = null) {
      var runningJob = GetRunningJob(jobId);
      if(runningJob != null)
        return; 
      var session = context.OpenSystemSession();
      var job = session.GetEntity<IJob>(jobId);
      Util.Check(job != null, "Job not found, ID: " + jobId);
      StartJob(job, sourceId); 
    }

    public void CancelJob(Guid jobId) {
      var jobCtx = GetRunningJob(jobId);
      if(jobCtx != null) {
        jobCtx.TryCancel();
      }
    }

    public IList<JobRunContext> GetRunningJobs() {
      return _runningJobs.Values.ToList();
    }

    public event EventHandler<JobNotificationEventArgs> Notify;

    private void OnJobNotify(JobRunContext jobContext, JobNotificationType notificationType) {
      Notify?.Invoke(this, new JobNotificationEventArgs() { Job = jobContext, NotificationType = notificationType });
    }
    #endregion

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

    #region Event handlers
    // Automatically set HasChildJobs flag on parent job
    private void SaveEvents_SavingChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      switch(record.Status) {
        case EntityStatus.New:
        case EntityStatus.Modified: break;
        default: return; 
      }
      var job = (IJob)record.EntityInstance;
      var parent = job.ParentJob; 
      if(parent == null || parent.Flags.IsSet(JobFlags.HasChildJobs))
        return;
      // set the flag 
      parent.Flags |= JobFlags.HasChildJobs; 
    }

    private void SaveEvents_SavedChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      if(record.StatusBeforeSave != Vita.Entities.Runtime.EntityStatus.New)
        return;
      var job = record.EntityInstance as IJob;
      if(!job.Flags.IsSet(JobFlags.StartOnSave))
        return;
      //We need to start new session here; if we use IJob's session, it will fail - 
      // StartJob will try to save more changes, but we are already inside SaveChanges
      var ctx = record.Session.Context;
      StartJob(ctx, job.Id);
    }

    // if false, we just started up
    bool _isRunning;
    bool _isRestarting; 

    private void Timers_Elapsed10Seconds(object sender, EventArgs e) {
      if(App.Status != EntityAppStatus.Connected)
        return;
      if(!_isRunning) {
        RestartJobRunsAfterRestart();
        _isRunning = true; 
      }
      // avoid entering twice; we don't use lock here, to avoid deadlock here. Simply do not enter if already entered
      if(_isRestarting)
        return; 
      try {
        _isRestarting = true; 
        RestartJobRunsDueForRetry();
      } finally {
        _isRestarting = false; 
      }
    }

    #endregion

  }//module
}
