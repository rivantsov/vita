using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  /// <summary>The service allows you to start and reliably execute sync or async tasks, with several retries in case of error. </summary>
  /// <remarks>
  /// You can define long-running processes, with progress reporting, or short asynchonous tasks with repeates after short/long periods of time. 
  /// The parameters of the method are deserialized on start from the database, and can be saved (serialized) when method completes with success or error. 
  /// </remarks>
  public interface IJobExecutionService {
    
    /// <summary> Starts a light job/task. The light job is a short-running task which is not initially persisted to database and the service tries 
    /// to execute it immediately. It is saved to the database only if it fails initially and needs to be retried later. </summary>
    /// <param name="context">Operation context.</param>
    /// <param name="func">The implementation function. </param>
    /// <param name="jobCode">Optional, job code (name).</param>
    /// <param name="eventId">Optional, ID of IEvent when started from scheduled event.</param>
    /// <param name="retryPolicy">Optiona, job retry policy. If not specified, JobRetryPolicy.DefaultLightTask is used.</param>
    /// <returns>The run context of the job.</returns>
    /// <remarks>
    /// <para> The implementation function <c>func</c> must be a call to static method (or instance method of EntityModule), for ex: </para>
    /// <para>   (jobRunContext) => SomeClass.DoTheWork(jobRunContext, "StringValue", 123, someObject) </para>
    /// <para> Alternatively, you can use JobHelper.ExecuteWithRetries helper method. </para>
    /// </remarks>
    Task<JobRunContext> RunLightTaskAsync(OperationContext context, Expression<Func<JobRunContext, Task>> func,
                     string jobCode, Guid? eventId = null, JobRetryPolicy retryPolicy = null);

    /// <summary>Creates a new job entity from Job definition object. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="job">Job definition.</param>
    /// <returns>Created IJob instance.</returns>
    /// <remarks>The database record is created when the caller invokes session.SaveChanges() method.
    /// If the job has a flag StartOnSave set, the job will be started immediately after the session changes are saved. 
    /// </remarks>
    IJob CreateJob(IEntitySession session, JobDefinition job);

    /// <summary>Creates a job that will be executed on a background (not pool) thread, likely long-running job. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="code">Name or code that identifies the job.</param>
    /// <param name="jobMethod">The expression representing a call to the worker method. </param>
    /// <param name="flags">Job flags.</param>
    /// <param name="retryPolicy">Retry policy.</param>
    /// <returns>The created job entity.</returns>
    /// <remarks>The job implementation method must be a call to a static or instance method returning void. 
    /// If an instance method is used, it must be defined on one of the global objects registered with the system - entity module, service, or object 
    /// registered with the EntityApp.RegisterGlobalObject call. 
    /// The only parameter of the delegate is a JobRunContext object that provides the context information about the running job to the implementation method. 
    /// If the job has a flag StartOnSave set, the job will be started immediately after the session changes are saved (parentJob parameter must be null in this case). 
    /// </remarks>
    IJob CreateBackgroundJob(IEntitySession session, string code, Expression<Action<JobRunContext>> jobMethod, JobFlags flags = JobFlags.Default, 
         JobRetryPolicy retryPolicy = null);

    /// <summary>Creates a job that will be executed on a pool thread.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="code">Name or code that identifies the job.</param>
    /// <param name="jobMethod">The expression representing a call to the worker method. Must be a sync or async method returning Task. </param>
    /// <param name="flags">Job flags.</param>
    /// <param name="retryPolicy">Retry policy.</param>
    /// <returns>The created job entity.</returns>
    /// <remarks>The job implementation method must be a call to a static or instance method returning Task. The method can be synchronuous or async. 
    /// If an instance method is used, it must be defined on one of the global objects registered with the system - entity module, service, or object 
    /// registered with the EntityApp.RegisterGlobalObject call. 
    /// The only parameter of the delegate is a JobRunContext object that provides the context information about the running job to the implementation method. 
    /// If the job has a flag StartOnSave set, the job will be started immediately after the session changes are saved (parentJob parameter must be null in this case). 
    /// </remarks>
    IJob CreatePoolJob(IEntitySession session, string code, Expression<Func<JobRunContext, Task>> jobMethod, 
          JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null);

    /// <summary>Starts a job identified by ID. </summary>
    /// <param name="context">Operation context.</param>
    /// <param name="jobId">Job ID.</param>
    /// <param name="eventId">Optional, source event ID if triggered by scheduled event.</param>
    void StartJob(OperationContext context, Guid jobId, Guid? eventId);

    /// <summary>Cancels a running job. </summary>
    /// <param name="jobId">Job ID.</param>
    void CancelJob(Guid jobId);

    /// <summary>Returns a list of currently running jobs. </summary>
    /// <returns>A list of running context objects for all jobs currently executing.</returns>
    /// <remarks>Includes only jobs that are currently in memory and executing. Does not include jobs that failed and waiting for retries.</remarks>
    IList<JobRunContext> GetRunningJobs();

    /// <summary>Returns a list of jobs that are currently executing or pending for restart in the future.</summary>
    /// <param name="session">Entity session.</param>
    /// <param name="maxJobs">Maximum jobs to return. If greater than 100, set to 100.</param>
    /// <returns>A list of active jobs.</returns>
    IList<IJobRun> GetActiveJobs(IEntitySession session, int maxJobs = 20);

    /// <summary>A notification event, fired when jobs are started, completed or failed. </summary>
    event EventHandler<JobNotificationEventArgs> Notify;
  }

  /// <summary>Job notification event arguments. </summary>
  public class JobNotificationEventArgs : EventArgs {
    public JobRunContext JobRunContext;
    public JobNotificationType NotificationType;
  }


}
