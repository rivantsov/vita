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

    #region IJobExecutionService members
    public async Task<JobRunContext> ExecuteWithRetriesAsync(OperationContext context, string jobName, Expression<Func<JobRunContext, Task>> func, RetryPolicy retryPolicy = null) {
      retryPolicy = retryPolicy ?? _settings.DefaultRetryPolicy;
      return await ExecuteJobAsync(context, jobName, func, retryPolicy);
    }

    public JobRunContext ExecuteWithRetriesNoWait(OperationContext context, string jobName, Expression<Action<JobRunContext>> action, RetryPolicy retryPolicy = null) {
      retryPolicy = retryPolicy ?? _settings.DefaultRetryPolicy;
      return ExecuteJobNoWait(context, jobName, action, retryPolicy);
    }

    public IJob CreateJob(IEntitySession session, string name, LambdaExpression lambda,
             JobThreadType threadType = JobThreadType.Pool, RetryPolicy retryPolicy = null) {
      retryPolicy = retryPolicy ?? _settings.DefaultRetryPolicy;
      var startInfo = CreateJobStartInfo(lambda, threadType);
      var job = NewJob(session, name, startInfo, retryPolicy);
      return job;
    }

    public IJobRun StartJobOnSaveChanges(IJob job, Guid? dataId = null, string data = null) {
      var utcNow = App.TimeService.UtcNow;
      var jobRun = NewJobRun(job, JobRunType.OnSave, utcNow, dataId, data);
      // Entity.SavedChanges handler will recognize the new (just inserted) IJobRun entity 
      // with status Executing and will start it
      jobRun.Status = JobRunStatus.Executing;
      jobRun.StartedOn = utcNow;
      return jobRun;  
    }

    public IJobRun ScheduleJobRunOn(IJob job, DateTime runOnUtc, Guid? dataId = null, string data = null, string hostName = null) {
      Util.Check(job != null, "Job parameter may not be null.");
      Util.Check(runOnUtc.Kind == DateTimeKind.Utc, "RunOn argument must be UTC time.");
      return NewJobRun(job, JobRunType.ScheduledDateTime, runOnUtc, dataId, data, hostName);
    }

    public IJobSchedule CreateJobSchedule(IJob job, string name, string cronSpec, DateTime? activeFrom, DateTime? activeUntil, string hostName = null) {
      Util.Check(job != null, "Job parameter may not be null.");
      return NewJobSchedule(job, name, cronSpec, activeFrom, activeUntil, hostName);
    }

    public int StartInterruptedJobsAfterAfterRestart(OperationContext context) {
      return StartInterruptedJobsAfterRestartImpl(context); 
    }

    #endregion 


    // Implementations --------------------------------------------------------------------------------------------------------------------------------
    private async Task<JobRunContext> ExecuteJobAsync(OperationContext context, string jobName, Expression<Func<JobRunContext, Task>> func, RetryPolicy retryPolicy) {
      jobName = CheckJobName(jobName);
      JobRunContext jobContext = null;
      try {
        var startInfo = CreateJobStartInfo(func, JobThreadType.Pool);
        jobContext = new JobRunContext(context, jobName, startInfo, JobRunType.Immediate);
        RegisterJobRun(jobContext);
        OnJobNotify(jobContext, JobNotificationType.Starting);
        //actually start
        var compiledFunc = func.Compile();
        await Task.Run(async () => await (Task)compiledFunc.Invoke(jobContext), jobContext.CancellationToken);
        // completed successfully
        jobContext.Status = JobRunStatus.Completed;
        UnregisterJobRun(jobContext);
        OnJobNotify(jobContext, JobNotificationType.Completed);
        return jobContext;
      } catch(OperationAbortException) {
        //soft exceptions (validation failed, smth like this) - we abort immediately, something wrong with input data, there's no point to repeat execution
        throw;  
      } catch(Exception agrEx) {
        var ex = GetInnerMostExc(agrEx);
        if(jobContext == null)
          throw new Exception("Failed to create JobRunContext for light task: " + ex.Message, ex);
        SaveJobAndJobRun(jobContext, retryPolicy, JobRunStatus.Error, ex);
        UnregisterJobRun(jobContext);
        OnJobNotify(jobContext, JobNotificationType.Error, ex);
        return jobContext;
      }
    }

    private JobRunContext ExecuteJobNoWait (OperationContext context, string jobName, Expression<Action<JobRunContext>> action, RetryPolicy retryPolicy) {
      jobName = CheckJobName(jobName);
      var startInfo = CreateJobStartInfo(action, JobThreadType.Pool);
      var jobContext = new JobRunContext(context, jobName, startInfo, JobRunType.Immediate);
      RegisterJobRun(jobContext);
      OnJobNotify(jobContext, JobNotificationType.Starting);
      //actually start
      var compiledFunc = action.Compile();
      var task1 = Task.Run(() => compiledFunc.Invoke(jobContext), jobContext.CancellationToken);
      var task2 = task1.ContinueWith((t) => NoWaitJobCompleted(t, jobContext, retryPolicy));
      return jobContext;
    }

    private Task NoWaitJobCompleted(Task mainTask, JobRunContext jobContext, RetryPolicy retryPolicy) {
      var realExc = mainTask.Exception == null ? null : GetInnerMostExc(mainTask.Exception);
      if (realExc == null) {
        jobContext.Status = JobRunStatus.Completed;
        UnregisterJobRun(jobContext);
        OnJobNotify(jobContext, JobNotificationType.Completed);
        return Task.CompletedTask;
      }
      // Job failed, realExc != null
      jobContext.Status = JobRunStatus.Error;
      if(jobContext.IsPersisted) {
        UpdateFailedJobRun(jobContext, realExc); 
      } else {
        SaveJobAndJobRun(jobContext, retryPolicy, JobRunStatus.Error, realExc);
      }
      UnregisterJobRun(jobContext);
      OnJobNotify(jobContext, JobNotificationType.Error);
      return Task.CompletedTask;
    }

  }//class
} //ns
