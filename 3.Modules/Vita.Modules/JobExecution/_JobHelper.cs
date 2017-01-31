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
    /// <remarks><para>The job implementation method must be a call to a static or instance async method. 
    /// If an instance method is used, it must be defined on one of the global objects - entity module, 
    /// service, or an object registered with the EntityApp.RegisterGlobalObject() call. 
    /// The only parameter of the delegate is a <c>JobRunContext</c> object that provides the context 
    /// of the running job to the implementation method.</para>
    /// <para>If OperationAbortException exception is thrown (ex: validation failed), the exception is rethrown to the caller, 
    /// and no retry attempts are made. </para>
    /// </remarks>
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

    /// <summary>Creates a job and starts it after session.SaveChanges() is called.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="jobName">Job name; free-form string to identify the job in the database.</param>
    /// <param name="jobMethod">Method to execute. Must be a call to a static or entity module method.</param>
    /// <param name="dataId">Optional, a Guid value to pass to executing job in JobRunContext.</param>
    /// <param name="data">Optional, a string value to pass to executing job in JobRunContext.</param>
    /// <param name="threadType">Thread type.</param>
    /// <param name="retryPolicy">Optional, retry policy.</param>
    /// <returns>Job run entity.</returns>
    /// <remarks>See remarks for method ExecuteWithRetriesNoWait for more information about job method.</remarks>
    public static IJobRun ScheduleJobRunOnSaveChanges(this IEntitySession session, string jobName, Expression<Action<JobRunContext>> jobMethod,
                                                  Guid? dataId = null, string data = null,  JobThreadType threadType = JobThreadType.Background, RetryPolicy retryPolicy = null) {
      var job = CreateJob(session, jobName, jobMethod, threadType, retryPolicy);
      var jobRun = job.ScheduleJobRunOnSaveChanges(dataId, data);
      return jobRun; 
    }

    /// <summary>Creates a job and a job run to start at a given UTC time.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="jobName">Job name; free-form string to identify the job in the database.</param>
    /// <param name="jobMethod">Method to execute. Must be a call to a static or entity module method.</param>
    /// <param name="runOnUtc">UTC date-time to run job at.</param>
    /// <param name="dataId">Optional, a Guid value to pass to executing job in JobRunContext.</param>
    /// <param name="data">Optional, a string value to pass to executing job in JobRunContext.</param>
    /// <param name="threadType">Thread type.</param>
    /// <param name="retryPolicy">Optional, retry policy.</param>
    /// <returns>Job run entity.</returns>
    /// <remarks>See remarks for method ExecuteWithRetriesNoWait for more information about job method.</remarks>
    public static IJobRun ScheduleJobRunOn(this IEntitySession session, string jobName,
            Expression<Action<JobRunContext>> jobMethod, DateTime runOnUtc, Guid? dataId = null, string data = null,
            JobThreadType threadType = JobThreadType.Background, RetryPolicy retryPolicy = null) {
      var service = session.Context.App.GetService<IJobExecutionService>();
      var job = service.CreateJob(session, jobName, jobMethod, threadType, retryPolicy);
      var jobRun = service.ScheduleJobRunOn(job, runOnUtc, dataId, data);
      return jobRun;
    }

    /// <summary>Creates a job run to start at a given UTC time.</summary>
    /// <param name="job">Job entity.</param>
    /// <param name="runOnUtc">UTC date-time to run job at.</param>
    /// <param name="dataId">Optional, a Guid value to pass to executing job in JobRunContext.</param>
    /// <param name="data">Optional, a string value to pass to executing job in JobRunContext.</param>
    /// <param name="hostName">Optional, name of the machine to execute the jobs. If null, the value is set to current machine name.</param>
    /// <returns>Job run entity.</returns>
    /// <remarks>See remarks for method ExecuteWithRetriesNoWait for more information about job method.</remarks>
    public static IJobRun ScheduleJobRunOn(this IJob job, DateTime runOnUtc, Guid? dataId = null, string data = null, string hostName = null) {
      var session = EntityHelper.GetSession(job);
      var service = session.Context.App.GetService<IJobExecutionService>();
      var jobRun = service.ScheduleJobRunOn(job, runOnUtc, dataId, data, hostName);
      return jobRun;
    }

    /// <summary>Creates a job run to start after session.SaveChanges() is called.</summary>
    /// <param name="job">Job entity.</param>
    /// <param name="dataId">Optional, a Guid value to pass to executing job in JobRunContext.</param>
    /// <param name="data">Optional, a string value to pass to executing job in JobRunContext.</param>
    /// <returns>Job run entity.</returns>
    /// <remarks>See remarks for method ExecuteWithRetriesNoWait for more information about job method.</remarks>
    public static IJobRun ScheduleJobRunOnSaveChanges(this IJob job, Guid? dataId = null, string data = null) {
      var session = EntityHelper.GetSession(job);
      var service = session.Context.App.GetService<IJobExecutionService>();
      var jobRun = service.StartJobOnSaveChanges(job, dataId, data);
      return jobRun;
    }

    /// <summary>Creates a job entity. The job can be scheduled later to run at certain time(s).</summary>
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
                                JobThreadType threadType = JobThreadType.Background, RetryPolicy retryPolicy = null) {
      var service = session.Context.App.GetService<IJobExecutionService>();
      var job = service.CreateJob(session, jobName, jobMethod, threadType, retryPolicy);
      return job; 
    }

    /// <summary>Creates a schedule for a repeated job. </summary>
    /// <param name="job">Job entity.</param>
    /// <param name="name">Schedule name.</param>
    /// <param name="cronSpec">CRON specification.</param>
    /// <param name="activeFrom">Optional, start date of the schedule.</param>
    /// <param name="activeUntil">Optional, end date of the schedule.</param>
    /// <param name="hostName">Optional, name of the machine to execute the scheduled jobs.</param>
    /// <returns>Job schedule entity.</returns>
    public static IJobSchedule CreateJobSchedule(this IJob job, string name, string cronSpec, 
                               DateTime? activeFrom = null, DateTime? activeUntil = null, string hostName = null) {
      var session = EntityHelper.GetSession(job);
      var service = session.Context.App.GetService<IJobExecutionService>();
      var sched = service.CreateJobSchedule(job, name, cronSpec, activeFrom, activeUntil, hostName);
      return sched;
    }

    /// <summary>Restarts jobs that were interrupted on last system shutdown. </summary>
    /// <param name="context">Operation context. In multi-tenant environment with separate databases
    /// determines the database to connect to and to check for interrupted jobs. </param>
    /// <returns>Number of restarted jobs.</returns>
    /// <remarks>Call this method after initializing/restarting the app and connecting it to database.</remarks>
    public static int StartInterruptedJobsAfterAfterRestart(OperationContext context) {
      var service = context.App.GetService<IJobExecutionService>();
      return service.StartInterruptedJobsAfterAfterRestart(context); 
    }

    /// <summary>Returns a job entity by unique name. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="jobName">Job name.</param>
    /// <returns>Job entity if found.</returns>
    /// <remarks>Job names does not have to be unique in the database. However, you may create specific reusable jobs with unique names
    /// and lookup them up by name. This method throws exception if a job is not found, or if it finds more than one match.</remarks>
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

    /// <summary>Returns the last finished (non-Pending) job run for a given job.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="jobId">Job ID.</param>
    /// <returns>Job run entity if found, null otherwise.</returns>
    public static IJobRun GetLastFinishedJobRun(this IEntitySession session, Guid jobId) {
      var jobRun = session.EntitySet<IJobRun>().Where(jr => jr.Job.Id == jobId)
          .Where(jr => jr.Status != JobRunStatus.Pending)
          .OrderByDescending(jr => jr.AttemptNumber).FirstOrDefault();
      return jobRun;
    }

    /// <summary>Returns the last finished (non-Pending) job run for a given job.</summary>
    /// <param name="job">Job entity.</param>
    /// <returns>Job run entity if found, null otherwise.</returns>
    public static IJobRun GetLastFinishedJobRun(this IJob job) {
      var session = EntityHelper.GetSession(job);
      return GetLastFinishedJobRun(session, job.Id); 
    }

    /// <summary>Returns a matching date-time for a given schedule after certain time.</summary>
    /// <param name="schedule">Job schedule entity.</param>
    /// <param name="afterDate">Start date to search for matching date.</param>
    /// <returns>Date-time of the next matching date. Returns null if no match - the date is outside the schedule&quot;s active period.</returns>
    public static DateTime? GetNextStartAfter(this IJobSchedule schedule, DateTime afterDate) {
      if(schedule.ActiveUntil != null && afterDate > schedule.ActiveUntil.Value)
        return null;
      if(afterDate < schedule.ActiveFrom)
        afterDate = schedule.ActiveFrom;
      var cron = new Cron.CronSchedule(schedule.CronSpec);
      var result = cron.TryGetNext(afterDate);
      if(result != null && schedule.ActiveUntil != null && result.Value > schedule.ActiveUntil.Value)
        result = null; 
      return result;
    }


  }//class 
} //namespace
