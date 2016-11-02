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

namespace Vita.Modules.JobExecution {

  public class JobExecutionModule : EntityModule, IJobExecutionService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    JsonSerializer _serializer;
    ITimerService _timers;
    IErrorLogService _errorLog;
    IIncidentLogService _incidentLog;
    DateTime? _nextJobOn; // null means 'reload jobs' to figure out next

    public JobExecutionModule(EntityArea area) : base(area, "JobRunner", version: CurrentVersion) {
      RegisterEntities(typeof(IJob));
      App.RegisterService<IJobExecutionService>(this); 
    } //constructor

    public override void Init() {
      base.Init();
      _timers = App.GetService<ITimerService>();
      _errorLog = App.GetService<IErrorLogService>();
      _incidentLog = App.GetService<IIncidentLogService>();
      _serializer = new JsonSerializer();
      //hook to events
      _timers.Elapsed1Minute += Timers_Elapsed1Minute;
      var ent = App.Model.GetEntityInfo(typeof(IJob));
      ent.SaveEvents.SavedChanges += SaveEvents_SavedChanges;
    }

    private void SaveEvents_SavedChanges(Entities.Runtime.EntityRecord record, EventArgs args) {
      // nullify NextJobOn to force job runner to reload all active jobs
      _nextJobOn = null; 
      if(record.StatusBeforeSave != Vita.Entities.Runtime.EntityStatus.New)
        return;
      var job = record.EntityInstance as IJob;
      if(!job.Flags.IsSet(JobFlags.StartOnSave))
        return;
      ExecuteJob(record.Session, job);
    }

    private void Timers_Elapsed1Minute(object sender, EventArgs e) {
      var utcNow = App.TimeService.UtcNow;
      var nextRun = _nextJobOn == null ? utcNow : _nextJobOn.Value;
      if(nextRun > utcNow)
        return;
      _nextJobOn = utcNow.AddSeconds(59); 
      //Load jobs due at this time
      var session = App.OpenSystemSession();
      var activeJobs = session.EntitySet<IJob>().Where(j => j.Status == JobStatus.Pending && j.NextRunOn <= utcNow && j.RetryCount > 0).ToList();
      foreach(var job in activeJobs)
        ExecuteJob(session, job);
      //update next job start; we assigned already _nextJobRun in this method, but new job could be created and set _nextJobOn = null
      // if _nextJobOn is still not null, query the database for earliest job 
      var next = _nextJobOn; 
      if (next != null) {
        var newNextRun = session.EntitySet<IJob>().Where(j => j.Status == JobStatus.Pending && j.RetryCount > 0).Min(j => j.NextRunOn);
        if(newNextRun > utcNow && _nextJobOn != null) //if it is in the future, set it as new next
          _nextJobOn = newNextRun;
      }          
    }

    private void ExecuteJob(IEntitySession session, IJob job) {
      //update job status
      var utcNow = App.TimeService.UtcNow; 
      var updateQuery = session.EntitySet<IJob>().Where(j => j.Id == job.Id).Select(j => 
                new { Status = JobStatus.Executing, RetryCount = j.RetryCount - 1, LastRunOn = utcNow });
      updateQuery.ExecuteUpdate<IJob>();
      if (job.Flags.IsSet(JobFlags.NonPoolThread)) {
        var thread = new Thread(JobThreadStart);
        thread.Start(job.Id);
      } else {
        Task.Run(() => JobThreadStart(job.Id));  
      }
    }

    private void JobThreadStart(object data) {
      var jobId = (Guid)data;
      var session = App.OpenSystemSession();
      var job = session.GetEntity<IJob>(jobId);
      // Start execution
      try {
        var jobCtx = new JobContext() { Session = session, Job = job };
        var jobRun = JobHelper.GetJobInfo(job, _serializer, jobCtx);
        object obj = null;
        //if method is not static, it is a module
        if(!jobRun.Method.IsStatic)
          obj = App.Modules.First(m => m.GetType() == jobRun.TargetType);
        jobRun.Method.Invoke(obj, jobRun.Arguments);
        //update job status
        var utcNow = App.TimeService.UtcNow; 
        var updateQuery = session.EntitySet<IJob>().Where(j => j.Id == job.Id).Select(j => 
                      new { Status = JobStatus.Completed, CompletedOn = utcNow });
        updateQuery.ExecuteUpdate<IJob>();
      } catch(Exception ex) {
        JobAttemptFailed(job.Id, ex); 
        //do not rethrow exc here
      }
    }

    private void JobAttemptFailed(Guid jobId, Exception exception) {
      // Open new fresh session, just in case there are invalid objects in old session
      var session = App.OpenSystemSession();
      var job = session.GetEntity<IJob>(jobId); 
      var errHeader = string.Format("=========================== Error {0} ======================================" + Environment.NewLine, App.TimeService.UtcNow);
      var errInfo = errHeader + exception.ToLogString();
      // If it is not final run, log it as an incident
      if(job.RetryCount > 0 && _incidentLog != null) 
        _incidentLog.LogIncident("JobFailed", message: exception.Message, key1: job.Code, keyId1: job.Id, notes: errInfo);
      else
        _errorLog.LogError(exception, session.Context);
      //Update job status 
      job.Errors += errInfo;
      job.CompletedOn = App.TimeService.UtcNow; 
      if (job.RetryCount > 0) {
        job.Status = JobStatus.Pending;
        job.NextRunOn = job.LastRunOn.Value.AddMinutes(job.RetryIntervalMinutes);
      } else 
        job.Status = JobStatus.Error;
      session.SaveChanges(); 
    }

    #region IJobExecutionService members
    public Guid CreateJob (IEntitySession session, string code, Expression<Action> lambda, JobFlags flags, int retryCount = 5, int retryIntervalMinutes = 5, Guid? ownerId = null) {
      var jobInfo = JobHelper.ParseCallExpression(lambda, _serializer);
      var ent = session.NewJob(code, jobInfo, retryCount, retryIntervalMinutes, ownerId);
      return ent.Id; 
    }
    #endregion 

  }//module
}
