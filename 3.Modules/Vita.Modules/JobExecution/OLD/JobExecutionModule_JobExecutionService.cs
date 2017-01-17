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

    public async Task<JobRunContext> RunLightTaskAsync(OperationContext context, Expression<Func<JobRunContext, Task>> func,
                     string jobCode, int[] retryIntervals = null) {
      JobRunContext jobCtx = null;
      try {
        jobCtx = new JobRunContext(this.App, _serializer, jobCode, JobFlags.IsLightJob);
        RegisterRunningJob(jobCtx);
        OnJobNotify(jobCtx, JobNotificationType.Starting);
        var compiledFunc = func.Compile();
        jobCtx.Task = Task.Run(() => (Task)compiledFunc.Invoke(jobCtx), jobCtx.CancellationToken);
        await jobCtx.Task;
        UnregisterRunningJob(jobCtx.JobId);
        jobCtx.Status = JobRunStatus.Completed;
        OnJobNotify(jobCtx, JobNotificationType.Completed);
        return jobCtx;
      } catch(Exception ex) {
        if(jobCtx == null)
          throw new Exception("Failed to create JobRunContext for light task: " + ex.Message, ex);
        //Failure is ok for now, it is expected eventually; just save the job for retries
        SaveLightTaskForRetries(context, jobCtx, func, jobCode, retryIntervals);
        //This will save exception info, update remaining counts, etc
        OnJobRunFinished(jobCtx, ex);
        return jobCtx;
      }
    }

    public void StartJob(OperationContext context, Guid jobId) {
      var runningJob = GetRunningJob(jobId);
      if(runningJob != null)
        return;
      var session = context.OpenSystemSession();
      var job = session.GetEntity<IJob>(jobId);
      Util.Check(job != null, "Job not found, ID: " + jobId);
      StartJob(job);
    }

    public void CancelJob(Guid jobId) {
      var jobCtx = GetRunningJob(jobId);
      if(jobCtx != null) {
        jobCtx.TryCancel();
      }
    }

    public IList<JobRunContext> GetRunningJobs() {
      return _runningJobs.Values.ToList();
    }
    public IList<Guid> GetRunningJobIds() {
      return _runningJobs.Values.Select(jr => jr.JobRunId).ToList();
    }

    public IList<IJobRun> GetActiveJobs(IEntitySession session, int maxJobs = 20) {
      maxJobs = Math.Max(20, 100);
      var query = session.EntitySet<IJobRun>().Include(jr => jr.Job).Where(jr => jr.Status == JobRunStatus.Executing || jr.Status == JobRunStatus.Failed)
        .Take(maxJobs).OrderBy(jr => jr.LastStartedOn);
      var result = query.ToList();
      return result;
    }


    public event EventHandler<JobNotificationEventArgs> Notify;

    private void OnJobNotify(JobRunContext jobContext, JobNotificationType notificationType, Exception exception = null) {
      Notify?.Invoke(this, new JobNotificationEventArgs() { JobRunContext = jobContext, NotificationType = notificationType, Exception = exception });
    }



  }//class
}
