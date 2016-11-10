using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  public partial class JobExecutionModule {

    private async Task<JobRunContext> RunLightTaskAsyncImpl(OperationContext context, Expression<Func<JobRunContext, Task>> func, string code, Guid? sourceId, JobRetryPolicy retryPolicy) {
      JobRunContext jobCtx = null;
      try {
        jobCtx = new JobRunContext(this.App, _serializer, code, sourceId);
        RegisterRunningJob(jobCtx);
        OnJobNotify(jobCtx, JobNotificationType.Starting);
        var compiledFunc = func.Compile();
        jobCtx.Task = Task.Run(() => (Task) compiledFunc.Invoke(jobCtx), jobCtx.CancellationToken);
        await jobCtx.Task;
        UnregisterRunningJob(jobCtx.JobId);
        jobCtx.Status = JobRunStatus.Completed;
        OnJobNotify(jobCtx, JobNotificationType.Completed);
        return jobCtx;
      } catch(Exception ex) {
        if(jobCtx == null)
          throw new Exception("Failed to create JobRunContext for light task: " + ex.Message, ex);
        //Failure is ok for now, it is expected eventually; just save the job for retries
        SaveFailedLightTask(context, jobCtx, func, code, sourceId, ex, retryPolicy ?? JobRetryPolicy.DefaultLightTask); 
        return jobCtx;
      }
    }

    private void SaveFailedLightTask(OperationContext originalOpContext, JobRunContext jobContext, Expression<Func<JobRunContext, Task>> func, 
             string code, Guid? sourceId, Exception exception, JobRetryPolicy retryPolicy) {
      try {
        var jobDef = JobDefinition.CreatePoolJob(code, func, jobContext.Flags, retryPolicy);
        var session = originalOpContext.OpenSystemSession();
        var job = session.NewJob(jobDef, _serializer);
        var jobRun = job.NewJobRun(sourceId);
        jobRun.Id = jobContext.JobRunId;
        jobRun.LastStartedOn = jobContext.StartedOn;
        jobRun.Progress = jobContext.Progress;
        jobRun.ProgressMessage = jobContext.ProgressMessage;
        jobContext.IsPersisted = true;
        session.SaveChanges();
        //This will save exception info, update remaining counts, etc
        UpdateFinishedJobRun(jobContext, exception);
      } catch(Exception fatalExc) {
        _errorLog.LogError(fatalExc, originalOpContext);
        throw; 
      }
    }

    private void StartJob(IJob job, Guid? sourceId) {
      //check if job is already running
      var jobCtx = GetRunningJob(job.Id);
      if(jobCtx != null)
        return; 
      var session = EntityHelper.GetSession(job);
      var jobRun = job.NewJobRun(sourceId);
      jobRun.LastStartedOn = session.Context.App.TimeService.UtcNow;
      jobRun.RunCount = 1; 
      session.SaveChanges();
      StartJobRun(jobRun); 
    }

    private void ShutdownJobs() {
      if(_runningJobs.Count == 0)
        return;
      var activeJobRunContexts = GetRunningJobs();
      foreach(var jobRunCtx in activeJobRunContexts) {
        jobRunCtx.TrySaveArguments();
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

    private void RestartJobRunsAfterRestart() {
      var utcNow = App.TimeService.UtcNow;
      // Find failed jobs to start at this time
      var session = App.OpenSystemSession();
      var jobRuns = session.EntitySet<IJobRun>().Include(jr => jr.Job)
        .Where(jr => jr.Status == JobRunStatus.Executing || jr.Status == JobRunStatus.Interrupted).ToList();
      if(jobRuns.Count == 0)
        return; 
      //update job run start time, status
      var ids = jobRuns.Select(jr => jr.Id).ToArray();
      var updateQuery = session.EntitySet<IJobRun>().Where(jr => ids.Contains(jr.Id))
               .Select(jr => new {Status = JobRunStatus.Executing, RunCount = jr.RunCount + 1, LastStartedOn = utcNow});
      updateQuery.ExecuteUpdate<IJobRun>();
      foreach(var jobRun in jobRuns)
        StartJobRun(jobRun);
    }

    private void RestartJobRunsDueForRetry() {
      var utcNow = App.TimeService.UtcNow;
      // Find failed jobs to start at this time
      var session = App.OpenSystemSession();
      // var allJobs = session.EntitySet<IJobRun>().ToList(); 
      var jobRuns = session.EntitySet<IJobRun>().Include(jr => jr.Job)
        .Where(jr => jr.Status == JobRunStatus.Failed && jr.NextStartOn != null && jr.NextStartOn <= utcNow).ToList();
      if(jobRuns.Count == 0)
        return; 
      //  remove jobs that are already running - just in case
      jobRuns = jobRuns.Where(jr => !_runningJobs.ContainsKey(jr.Job.Id)).ToList(); 
      //update job run statuses to executing
      var ids = jobRuns.Select(jr => jr.Id).ToArray(); 
      var updateQuery = session.EntitySet<IJobRun>().Where(jr => ids.Contains(jr.Id))
               .Select(jr => new {
                 Status = JobRunStatus.Executing, LastStartedOn = utcNow, RunCount = jr.RunCount + 1
               });
      updateQuery.ExecuteUpdate<IJobRun>();
      foreach(var jobRun in jobRuns)
        StartJobRun(jobRun); 
   }

    private void StartJobRun(IJobRun jobRun) {
      //create job context 
      var jobCtx = new JobRunContext(this.App, jobRun, _serializer);
      RegisterRunningJob(jobCtx);
      OnJobNotify(jobCtx, JobNotificationType.Starting);
      jobCtx.StartInfo = JobUtil.GetJobStartInfo(jobRun, jobCtx);
      if(jobRun.Job.ThreadType == ThreadType.Background) {
        jobCtx.Thread = new Thread(RunBackgroundJob);
        jobCtx.Thread.Start(jobCtx); 
      } else {
        Task.Run(() => StartPoolJob(jobCtx));
      }
    }

    private void RunBackgroundJob(object data) {
      var jobCtx = (JobRunContext)data;
      try {
        var startInfo = jobCtx.StartInfo; 
        startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments);
        UpdateFinishedJobRun(jobCtx);
      } catch(Exception ex) {
        UpdateFinishedJobRun(jobCtx, ex);
        //do not rethrow exc here
      } 
    }

    private void StartPoolJob(object data) {
      JobRunContext jobCtx = (JobRunContext)data;
      try {
        var startInfo = jobCtx.StartInfo;
        bool startAsync = startInfo.Method.ReturnType.IsAssignableFrom(typeof(Task));
        if(startAsync) {
          var task = (Task)startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments);
          task = task ?? Task.CompletedTask; //method might be sync and return null as Task result
          var task2 = task.ContinueWith((t) => OnAsyncJobRunCompleted(t, jobCtx));
          Task.WaitAll(task2); //.Run(() => task2);
          // do not await the task here
        } else {
          startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments);
          UpdateFinishedJobRun(jobCtx);
        }
      } catch(Exception ex) {
        UpdateFinishedJobRun(jobCtx, ex);
        //do not rethrow exc here
      }
    }


    private Task OnAsyncJobRunCompleted(Task mainTask, JobRunContext jobContext) {
      var realExc = (mainTask.Exception == null) ? null : mainTask.Exception.InnerExceptions[0];
      UpdateFinishedJobRun(jobContext, realExc);
      return Task.CompletedTask;
    }

    private void UpdateFinishedJobRun(JobRunContext jobContext, Exception exception = null) {
      try {
        UpdateFinishedJobRunImpl(jobContext, exception); 
      } catch(Exception newExc) {
        _errorLog.LogError(newExc, jobContext.OperationContext);
        if(exception != null)
          _errorLog.LogError(exception, jobContext.OperationContext);
      }
    }

    private void UpdateFinishedJobRunImpl(JobRunContext jobContext, Exception exception = null) {
      var jobId = jobContext.JobId;
      UnregisterRunningJob(jobId); 
      var session = jobContext.OperationContext.OpenSystemSession();
      var jobRun = session.GetEntity<IJobRun>(jobContext.JobRunId);
      var utcNow = session.Context.App.TimeService.UtcNow;
      jobRun.LastEndedOn = utcNow;
      if(jobContext.Flags.IsSet(JobFlags.PersistArguments))
        jobRun.CurrentArguments = JobUtil.SerializeArguments(jobContext.StartInfo.Arguments, _serializer);
      if(exception == null) {
        jobRun.NextStartOn = null;
        jobRun.Status = jobContext.Status = JobRunStatus.Completed;
        jobContext.Status = JobRunStatus.Completed;
        session.SaveChanges();
        OnJobNotify(jobContext, JobNotificationType.Completed); 
        if (jobRun.Job.Flags.IsSet(JobFlags.HasChildJobs)) //flag is set automatically when saving child jobs
          StartChildJobs(jobRun.Job, jobRun.SourceId); 
        return; 
      } 
      // Current run ended with error
      ReportJobRunError(jobRun, exception);
      OnJobNotify(jobContext, JobNotificationType.Error);
      // current run failed; if we have no more retries, mark as error
      if(jobRun.RemainingRetries == 0 && jobRun.RemainingRounds == 0) {
        jobRun.Status = jobContext.Status = JobRunStatus.Error;
        jobRun.NextStartOn = null;
        session.SaveChanges(); 
        return; 
      }
      // current run failed, but we have retries
      jobRun.Status = jobContext.Status = JobRunStatus.Failed;
      if (jobRun.RemainingRetries > 0) {
        jobRun.NextStartOn = utcNow.AddSeconds(jobRun.Job.RetryIntervalSec);
        jobRun.RemainingRetries--;
      } else {
        jobRun.RemainingRounds--;
        jobRun.RemainingRetries = jobRun.Job.RetryCount - 1;
        jobRun.NextStartOn = utcNow.AddMinutes(jobRun.Job.RetryPauseMinutes);
      }
      session.SaveChanges(); 
    }//method

    private void ReportJobRunError(IJobRun jobRun, Exception exception) {
      var session = EntityHelper.GetSession(jobRun); 
      var errHeader = string.Format("=========================== Error {0} ======================================" 
             + Environment.NewLine, App.TimeService.UtcNow);
      var errMsg = errHeader + exception.ToLogString();
      // If it is not final run, log it as an incident
      var job = jobRun.Job;
      bool hasRetries = jobRun.RemainingRetries > 0 || jobRun.RemainingRounds > 0; 
      if(hasRetries && _incidentLog != null)
        _incidentLog.LogIncident("JobRunFailed", message: exception.Message, key1: job.Code, keyId1: job.Id, notes: errMsg);
      else
        _errorLog.LogError(exception, session.Context);
      //Update job status 
      jobRun.Errors += errMsg + Environment.NewLine;
    }

    private void StartChildJobs(IJob job, Guid? sourceId) {
      var session = EntityHelper.GetSession(job);
      var childJobs = session.EntitySet<IJob>().Where(j => j.ParentJob == job).ToList();
      if(childJobs.Count == 0)
        return;
      foreach(var ch in childJobs)
        StartJob(job, sourceId); 
    }

  }//class
}//ns
