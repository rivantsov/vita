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
    
    /// <summary> Starts a light job/task. The light job is a short-running task which is not initially persisted to database. It is saved to the database 
    /// only if it fails initially and needs to be retried later. </summary>
    /// <param name="context">Operation context.</param>
    /// <param name="func">The implementation function. </param>
    /// <param name="jobCode">Optional, job code (name).</param>
    /// <param name="sourceId">Optional, source object ID, for example Calendar event ID.</param>
    /// <returns>The run context of the job.</returns>
    /// <remarks>
    /// <para> The implementation function <c>func</c> must be a call to static method (or instance method of EntityModule), for ex: </para>
    /// <para> (jobRunContext) => SomeClass.DoTheWork(jobRunContext, "StringValue", 123, someObject) </para>
    /// </remarks>
    Task<JobRunContext> RunLightTaskAsync(OperationContext context, Expression<Func<JobRunContext, Task>> func, string jobCode = "None", Guid? sourceId = null);
    
    /// <summary>Creates a job record in the database. If the job has a flag StartOnSave set, the job will be started immediately after 
    /// session changes are saved. </summary>
    /// <param name="session">Entity session.</param>
    /// <param name="job">Job definition.</param>
    /// <returns>Created IJob instance.</returns>
    IJob CreateJob(IEntitySession session, JobDefinition job);

    /// <summary>Starts a job identified by ID. </summary>
    /// <param name="context">Operation context.</param>
    /// <param name="jobId">Job ID.</param>
    /// <param name="sourceId">Optional, source object ID, for example Calendar event ID.</param>
    void StartJob(OperationContext context, Guid jobId, Guid? sourceId);

    /// <summary>Cancels a running job. </summary>
    /// <param name="jobId">Job ID.</param>
    void CancelJob(Guid jobId);

    /// <summary>Returns a list of currently running jobs. </summary>
    /// <returns>A list of running context objects for all jobs currently executing.</returns>
    IList<JobRunContext> GetRunningJobs();

    /// <summary>A notification event, fired when jobs are started, completed or failed. </summary>
    event EventHandler<JobNotificationEventArgs> Notify;
  }

  /// <summary>Job notification event arguments. </summary>
  public class JobNotificationEventArgs : EventArgs {
    public JobRunContext Job;
    public JobNotificationType NotificationType;
  }


}
