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

  public static class JobHelper {
    /// <summary>Executes light async task (job). The job is persisted only if initial attempt fails.</summary>
    /// <param name="context">Operation context.</param>
    /// <param name="jobName">Job name; free-form string to identify the job in the database.</param>
    /// <param name="jobMethod">Function to execute. Must be a call to a static or entity module method.</param>
    /// <param name="retryPolicy">Optional, retry policy.</param>
    /// <returns>JobRunContext identifying the job.</returns>
    /// <remarks>The job implementation method must be a call to a static or instance async method. 
    /// If an instance method is used, it must be defined on one of the global objects - entity module, 
    /// service, or an object registered with the EntityApp.RegisterGlobalObject() call. 
    /// The only parameter of the delegate is a <c>JobRunContext</c> object that provides the context 
    /// of the running job to the implementation method. </remarks>
    public static async Task<JobRunContext> ExecuteWithRetriesAsync(OperationContext context, string jobName, 
                   Expression<Func<JobRunContext, Task>> jobMethod, RetryPolicy retryPolicy = null) {
      var jobService = context.App.GetService<IJobExecutionService>();
      Util.Check(jobService != null, "IJobExecutionService not registered with the entity app; add JobExecutionModule.");
      return await jobService.ExecuteWithRetriesAsync(context, jobName, jobMethod, retryPolicy);
    }

    /// <summary>Starts a reliable task with retries on a pool thread and returns immmediately. The job is persisted only if initial attempt fails.</summary>
    /// <param name="context">Operaton context.</param>
    /// <param name="jobName">Job name; free-form string to identify the job in the database.</param>
    /// <param name="jobMethod">Method to execute. Must be a call to a static or entity module method.</param>
    /// <param name="retryPolicy">Optional, retry policy.</param>
    /// <returns>Job run context identifying the task.</returns>
    /// <remarks>The job implementation method must be a call to a static or instance method returning void. 
    /// If an instance method is used, it must be defined on one of the global objects - entity module, 
    /// service, or an object registered with the EntityApp.RegisterGlobalObject() call. 
    /// The only parameter of the delegate is a <c>JobRunContext</c> object that provides the context 
    /// of the running job to the implementation method. </remarks>
    public static JobRunContext ExecuteWithRetriesNoWait(OperationContext context, string jobName, Expression<Action<JobRunContext>> jobMethod,
                                                  RetryPolicy retryPolicy = null) {
      var jobService = context.App.GetService<IJobExecutionService>();
      Util.Check(jobService != null, "IJobExecutionService not registered with the entity app; add JobExecutionModule.");
      return jobService.ExecuteWithRetriesNoWait(context, jobName, jobMethod, retryPolicy);
    }

    /// <summary>Starts a reliable task with retries on a pool thread and returns immmediately. The job is persisted only if initial attempt fails.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="jobName">Job name; free-form string to identify the job in the database.</param>
    /// <param name="jobMethod">Method to execute. Must be a call to a static or entity module method.</param>
    /// <param name="dataId">Optional, a Guid value to pass to executing job in JobRunContext.</param>
    /// <param name="data">Optional, a string value to pass to executing job in JobRunContext.</param>
    /// <param name="threadType">Thread type.</param>
    /// <param name="retryPolicy">Optional, retry policy.</param>
    /// <returns>Job run context identifying the task.</returns>
    /// <remarks>The job implementation method must be a call to a static or instance method returning void. 
    /// If an instance method is used, it must be defined on one of the global objects - entity module, 
    /// service, or an object registered with the EntityApp.RegisterGlobalObject() call. 
    /// The only parameter of the delegate is a <c>JobRunContext</c> object that provides the context 
    /// of the running job to the implementation method. </remarks>
    public static IJobRun ExecuteWithRetriesOnSaveChanges(this IEntitySession session, string jobName, Expression<Action<JobRunContext>> jobMethod,
                                                  Guid? dataId = null, string data = null,  JobThreadType threadType = JobThreadType.Pool, RetryPolicy retryPolicy = null) {
      var job = CreateJob(session, jobName, jobMethod, threadType, retryPolicy);
      var jobRun = job.StartJobOnSaveChanges(dataId, data);
      return jobRun; 
    }



    /// <summary>Creates a job entity.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="jobName">Name or code that identifies the job.</param>
    /// <param name="jobMethod">The expression representing a call to the worker method. Must be a sync or async method returning Task. </param>
    /// <param name="threadType">Thread type.</param>
    /// <param name="retryPolicy">Optional, retry policy.</param>
    /// <returns>The created job entity.</returns>
    /// <remarks>The job implementation method must be a call to a static or instance void method. 
    /// See remarks for ExecuteWithRetries methods for more about implementation method. 
    /// </remarks>
    public static IJob CreateJob(this IEntitySession session, string jobName, Expression<Action<JobRunContext>> jobMethod,
                                JobThreadType threadType = JobThreadType.Pool, RetryPolicy retryPolicy = null) {
      var service = session.Context.App.GetService<IJobExecutionService>();
      var job = service.CreateJob(session, jobName, jobMethod, threadType, retryPolicy);
      return job; 
    }

    public static IJobRun StartJobOnSaveChanges(this IJob job, Guid? dataId = null, string data = null) {
      var session = EntityHelper.GetSession(job);
      var service = session.Context.App.GetService<IJobExecutionService>();
      var jobRun = service.StartJobOnSaveChanges(job, dataId, data);
      return jobRun;
    }


    public static IJobRun ScheduleJobRunOn(this IJob job, DateTime runOnUtc, Guid? dataId = null, string data = null) {
      var session = EntityHelper.GetSession(job);
      var service = session.Context.App.GetService<IJobExecutionService>();
      var jobRun = service.ScheduleJobRunOn(job, runOnUtc, dataId, data);
      return jobRun;
    }

    public static IJobSchedule SetJobSchedule(IJob job, string cronSpec, DateTime? activeFrom = null, DateTime? activeUntil = null) {
      var session = EntityHelper.GetSession(job);
      var service = session.Context.App.GetService<IJobExecutionService>();
      var sched = service.SetJobSchedule(job, cronSpec, activeFrom, activeUntil);
      return sched;
    }

    // Extensions
    public static IJob GetJobByUniqueName(this IEntitySession session, string jobName) {
      var jobs = session.EntitySet<IJob>().Where(j => j.Name == jobName).Take(2).ToList();
      switch(jobs.Count) {
        case 0:
          Util.Check(false, "Job {0} not found.");
          break; 
        case 1:
          return jobs[0];
        default:
          Util.Check(false, "More than one job found with name '{0}'.", jobName);
          break; 
      }
      return null; //never happens
    }

    public static IJobRun GetLastFinishedJobRun(this IEntitySession session, Guid jobId) {
      var jobRun = session.EntitySet<IJobRun>().Where(jr => jr.Job.Id == jobId)
          .Where(jr => jr.Status != JobRunStatus.Pending)
          .OrderByDescending(jr => jr.AttemptNumber).FirstOrDefault();
      return jobRun;
    }

    public static IJobRun GetLastFinishedJobRun(this IJob job) {
      var session = EntityHelper.GetSession(job);
      return GetLastFinishedJobRun(session, job.Id); 
    }

    public static bool IsSet(this JobFlags flags, JobFlags flag) {
      return (flags & flag) != 0;
    }


  }//class 
} //namespace
