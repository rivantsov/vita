using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {
  public partial class JobExecutionModule {

    private void SaveJobAndJobRun(JobRunContext jobContext, RetryPolicy retryPolicy, JobRunStatus status, Exception exception = null) {
      var session = jobContext.OperationContext.OpenSession();
      var job = NewJob(session, jobContext.JobName, jobContext.StartInfo, retryPolicy);
      job.Id = jobContext.JobId;
      var jobRun = job.NewJobRun();
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
      job.Flags = JobFlags.None;
      job.ThreadType = startInfo.ThreadType; 
      job.DeclaringType = startInfo.DeclaringType.Namespace + "." + startInfo.DeclaringType.Name;
      job.MethodName = startInfo.Method.Name;
      job.MethodParameterCount = startInfo.Arguments.Length;
      job.Arguments = SerializeArguments(startInfo.Arguments);
      job.RetryIntervals = retryPolicy.AsString;
      job.HostName = _settings.HostName;
      return job;
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
      // current run failed; if we have no more retries, mark as error
      jobRun.Status = jobContext.Status = JobRunStatus.Error;
      // get wait time for next attempt
      var waitMinutes = JobUtil.GetWaitInterval(jobRun.Job.RetryIntervals, jobRun.AttemptNumber + 1);
      var hasRetries = waitMinutes > 0; 
      if(hasRetries) {
        // create job run for retry
        var nextTry = CreateRetryRun(jobRun, waitMinutes);
      }
      ReportJobRunFailure(jobRun, exception, hasRetries);
    }

    private void ReportJobRunFailure(IJobRun jobRun, Exception exception, bool hasRetries) {
      var session = EntityHelper.GetSession(jobRun);
      var errHeader = string.Format("=========================== Error {0} ======================================"
             + Environment.NewLine, App.TimeService.UtcNow);
      var errMsg = errHeader + exception.ToLogString();
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
      var retryRun = jobRun.Job.NewJobRun(retryOn);
      retryRun.AttemptNumber = jobRun.AttemptNumber + 1;
      retryRun.UserId = jobRun.UserId;
      return retryRun; 
    }



  }
}
