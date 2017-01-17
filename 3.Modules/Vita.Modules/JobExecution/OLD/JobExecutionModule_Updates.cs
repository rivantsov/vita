using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  public partial class JobExecutionModule {

    // saves the task itself for retries after it failed initially
    private void SaveLightTaskForRetries(OperationContext originalOpContext, JobRunContext jobContext, Expression<Func<JobRunContext, Task>> func,
                                               string code, int[] retryIntervals = null) {
      try {
        var jobDef = JobDefinition.CreatePoolJob(code, func, jobContext.Flags, retryIntervals);
        var session = originalOpContext.OpenSystemSession();
        var job = session.NewJob(jobDef, _serializer);
        var jobRun = job.NewJobRun();
        jobRun.Id = jobContext.JobRunId;
        jobRun.EndedOn = originalOpContext.App.TimeService.UtcNow; 
        jobRun.Progress = jobContext.Progress;
        jobRun.ProgressMessage = jobContext.ProgressMessage;
        jobContext.IsPersisted = true;
        session.SaveChanges();
      } catch(Exception fatalExc) {
        _errorLog.LogError(fatalExc, originalOpContext);
        throw;
      }
    }


    private void UpdateFinishedJobRun(JobRunContext jobContext, Exception exception = null) {
      try {
        UnregisterRunningJob(jobContext.JobId);
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
      if(jobContext.Flags.IsSet(JobFlags.PersistArguments))
        jobRun.CurrentArguments = JobUtil.SerializeArguments(jobContext.StartInfo.Arguments, _serializer);
      jobRun.Status = jobContext.Status = JobRunStatus.Completed;
      jobContext.Status = JobRunStatus.Completed;
      session.SaveChanges();
    }//method

    private void UpdateFailedJobRun(JobRunContext jobContext, Exception exception) {
      var session = jobContext.OperationContext.OpenSystemSession();
      var jobRun = session.GetEntity<IJobRun>(jobContext.JobRunId);
      var utcNow = session.Context.App.TimeService.UtcNow;
      jobRun.EndedOn = utcNow;
      ReportJobRunFailure(jobRun, exception);
      // current run failed; if we have no more retries, mark as error
      if(jobRun.RemainingRetries == 0 && jobRun.RemainingRounds == 0) {
        jobRun.Status = jobContext.Status = JobRunStatus.Error;
        jobRun.NextStartOn = null;
        session.SaveChanges();
        return;
      }
      // current run failed, but we have retries
      jobRun.Status = jobContext.Status = JobRunStatus.Failed;
      if(jobRun.RemainingRetries > 0) {
        jobRun.NextStartOn = utcNow.AddSeconds(jobRun.Job.RetryIntervalSec);
        jobRun.RemainingRetries--;
      } else {
        jobRun.RemainingRounds--;
        jobRun.RemainingRetries = jobRun.Job.RetryCount - 1;
        jobRun.NextStartOn = utcNow.AddMinutes(jobRun.Job.RetryPauseMinutes);
      }
      session.SaveChanges();
    }

    private void ReportJobRunFailure(IJobRun jobRun, Exception exception) {
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


  }//class
}
