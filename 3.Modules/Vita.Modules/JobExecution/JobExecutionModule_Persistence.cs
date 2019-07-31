using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.JobExecution {
  public partial class JobExecutionModule {

    private void SaveJobAndJobRun(JobRunContext jobContext, RetryPolicy retryPolicy, JobRunStatus status, Exception exception = null) {
      var session = jobContext.OperationContext.OpenSession();
      var job = NewJob(session, jobContext.JobName, jobContext.StartInfo, retryPolicy);
      job.Id = jobContext.JobId;
      var jobRun = NewJobRun(job, JobRunType.Immediate);
      jobRun.Id = jobContext.JobRunId;
      jobContext.IsPersisted = true;
      jobRun.Status = status;
      jobRun.StartedOn = jobContext.StartedOn; 
      if(exception != null)
        UpdateFailedJobRun(session, jobRun, jobContext, exception);
      session.SaveChanges();
    }

    private IJob NewJob(IEntitySession session, string name, JobStartInfo startInfo, RetryPolicy retryPolicy) {
      var job = session.NewEntity<IJob>();
      job.Name = name;
      job.ThreadType = startInfo.ThreadType; 
      job.DeclaringType = startInfo.DeclaringType.Namespace + "." + startInfo.DeclaringType.Name;
      job.MethodName = startInfo.Method.Name;
      job.MethodParameterCount = startInfo.Arguments.Length;
      job.Arguments = SerializeArguments(startInfo.Arguments);
      job.RetryIntervals = retryPolicy.AsString;
      return job;
    }

    internal IJobRun NewJobRun(IJob job, JobRunType runType, DateTime? startOn = null, Guid? dataId = null, string data = null, string hostName = null) {
      var session = EntityHelper.GetSession(job);
      var timeService = session.Context.App.TimeService;
      var jobRun = session.NewEntity<IJobRun>();
      jobRun.Job = job;
      jobRun.RunType = runType;
      jobRun.StartOn = startOn == null ? timeService.UtcNow : startOn.Value;
      jobRun.DataId = dataId;
      jobRun.Data = data;
      jobRun.AttemptNumber = 1;
      jobRun.UserId = session.Context.User.UserId;
      jobRun.HostName = hostName ?? _settings.HostName;
      return jobRun;
    }

    internal IJobSchedule NewJobSchedule(IJob job, string name, string cronSpec, DateTime? activeFrom, DateTime? activeUntil, string hostName = null) {
      Util.Check(job.Schedule == null, "Job '{0}' already has a schedule assigned, cannot create another one.", job.Name);
      var session = EntityHelper.GetSession(job);
      var timeService = session.Context.App.TimeService;
      var sched = session.NewEntity<IJobSchedule>();
      sched.Job = job;
      sched.Name = name;
      sched.CronSpec = cronSpec;
      sched.ActiveFrom = activeFrom == null ? timeService.UtcNow : activeFrom.Value;
      sched.ActiveUntil = activeUntil;
      sched.HostName = hostName ?? _settings.HostName; 
      return sched;
    }

    private void UpdateFinishedJobRun(JobRunContext jobContext, Exception exception = null) {
      try {
        if(exception == null)
          UpdateSuccessfulJobRun(jobContext);
        else
          UpdateFailedJobRun(jobContext, exception);
      } catch(Exception newExc) {
        _errorLog.LogError(newExc, jobContext.OperationContext);
        if(exception != null)
          _errorLog.LogError(exception, jobContext.OperationContext);
      }
    }

    private void UpdateSuccessfulJobRun(JobRunContext jobContext) {
      var session = jobContext.OperationContext.OpenSystemSession();
      var jobRun = session.GetEntity<IJobRun>(jobContext.JobRunId);
      var utcNow = session.Context.App.TimeService.UtcNow;
      jobRun.EndedOn = utcNow;
      jobRun.Status = jobContext.Status = JobRunStatus.Completed;
      jobContext.Status = JobRunStatus.Completed;
      session.SaveChanges();
    }//method

    private void UpdateFailedJobRun(JobRunContext jobContext, Exception exception) {
      var session = jobContext.OperationContext.OpenSystemSession();
      var jobRun = session.GetEntity<IJobRun>(jobContext.JobRunId);
      UpdateFailedJobRun(session, jobRun, jobContext, exception);
      session.SaveChanges();
    }

    private void UpdateFailedJobRun(IEntitySession session, IJobRun jobRun, JobRunContext jobContext, Exception exception) {
      var utcNow = session.Context.App.TimeService.UtcNow;
      jobRun.EndedOn = utcNow;
      // current run failed; mark as error
      jobRun.Status = jobContext.Status = JobRunStatus.Error;
      string customNote = null; 
      // if exception is soft exc (validation failure) - do not retry
      bool isOpAbort = exception is OperationAbortException;
      if(isOpAbort)
        //This will apear in log
        customNote = "Attempt resulted in OperationAbort exception, no retries are scheduled.";
      var hasRetries = !isOpAbort; 
      if (hasRetries) {
        // get wait time for next attempt
        var waitMinutes = GetWaitInterval(jobRun.Job.RetryIntervals, jobRun.AttemptNumber + 1);
        hasRetries &= waitMinutes > 0;
        if (hasRetries) {
          // create job run for retry
          var nextTry = CreateRetryRun(jobRun, waitMinutes);
        }
      }
      ReportJobRunFailure(jobRun, exception, hasRetries, customNote);
    }

    private void ReportJobRunFailure(IJobRun jobRun, Exception exception, bool hasRetries, string customNote) {
      var session = EntityHelper.GetSession(jobRun);
      var errHeader = string.Format("=========================== Error {0} ======================================"
             + Environment.NewLine, App.TimeService.UtcNow);
      var errMsg = errHeader + exception.ToLogString();
      if(!string.IsNullOrEmpty(customNote))
        errMsg += Environment.NewLine + customNote;
      // If it is not final run, log it as an incident
      var job = jobRun.Job;
      if(hasRetries && _incidentLog != null)
        _incidentLog.LogIncident("JobRunFailed", message: exception.Message, key1: job.Name, keyId1: job.Id, notes: errMsg);
      else
        _errorLog.LogError(exception, session.Context);
      //Update job log 
      jobRun.Log += errMsg + Environment.NewLine;
    }

    public IJobRun CreateRetryRun(IJobRun jobRun, int waitMinutes) {
      var session = EntityHelper.GetSession(jobRun);
      var baseTime = jobRun.EndedOn ?? jobRun.StartedOn ?? App.TimeService.UtcNow;
      var retryOn = baseTime.AddMinutes(waitMinutes); 
      var retryRun = NewJobRun(jobRun.Job, JobRunType.Retry, retryOn, jobRun.DataId, jobRun.Data, jobRun.HostName);
      retryRun.AttemptNumber = jobRun.AttemptNumber + 1;
      retryRun.UserId = jobRun.UserId;
      retryRun.Status = JobRunStatus.Pending; 
      return retryRun; 
    }

    private void OnJobScheduleSaving(IJobSchedule sched) {
      var session = EntityHelper.GetSession(sched);
      var job = sched.Job;
      var utcNow = session.Context.App.TimeService.UtcNow;
      var nextRunId = sched.NextRunId;
      IJobRun nextRun = (nextRunId == null) ? null : session.GetEntity<IJobRun>(nextRunId.Value);
      switch(sched.Status) {
        case JobScheduleStatus.Stopped:
          // if there is a pending run in the future, kill it
          if(nextRun != null && nextRun.Status == JobRunStatus.Pending && nextRun.StartOn > utcNow.AddMinutes(1))
            nextRun.Status = JobRunStatus.Killed;
          break;
        case JobScheduleStatus.Active:
          // Create or adjust JobRun entity for next run
          var nextStartOn = sched.GetNextStartAfter(utcNow);
          if(nextStartOn != null) {
            if(nextRun == null || nextRun.Status != JobRunStatus.Pending) {
              nextRun = NewJobRun(job, JobRunType.Schedule, nextStartOn, hostName: sched.HostName);
              sched.NextRunId = nextRun.Id;
            } else
              nextRun.StartOn = nextStartOn.Value;
          } else
            //nextSTartOn == null
            sched.NextRunId = null;
          break;
      }//switch
    }//method

  }
}
