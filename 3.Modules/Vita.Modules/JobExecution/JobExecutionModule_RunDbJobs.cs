using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.JobExecution {
  public partial class JobExecutionModule {

    private void StartDueJobRuns() {
      var utcNow = App.TimeService.UtcNow;
      var ctxList = GetContextsForConnectedDataSources();
      foreach(var ctx in ctxList)
        StartDueJobRuns(ctx, utcNow);
    }

    private void StartDueJobRuns(OperationContext context, DateTime utcNow) {
      const int BatchSize = 100;
      while(true) {
        var session = context.OpenSystemSession();
        // 1. Get batch of up to 100 JobRuns due
        var jobRuns = session.EntitySet<IJobRun>()
          .Include(jr => jr.Job)
          .Where(jr => jr.Status == JobRunStatus.Pending && jr.StartOn <= utcNow && 
                 jr.HostName == _settings.HostName)
          .Take(BatchSize)
          .ToList();
        if(jobRuns.Count == 0)
          return;
        //2. Updates: create next runs for scheduled runs, update job run statuses to executing
        CreateNextRunsForScheduledRuns(session, jobRuns, utcNow);
        var ids = jobRuns.Select(jr => jr.Id).ToArray();
        var updateQuery = session.EntitySet<IJobRun>().Where(jr => ids.Contains(jr.Id))
                 .Select(jr => new {
                   Status = JobRunStatus.Executing, StartedOn = utcNow
                 });
        // updateQuery.ExecuteUpdate<IJobRun>();
        session.ScheduleUpdate<IJobRun>(updateQuery);
        session.SaveChanges(); 
        //3. Actually start job runs
        foreach(var jobRun in jobRuns)
          StartJobRun(jobRun);
        // If we got the last record, exit
        if(jobRuns.Count < BatchSize)
          return;
      } //while
    } // method

    private void CreateNextRunsForScheduledRuns(IEntitySession session, IList<IJobRun> jobRuns, DateTime utcNow) {
      //Find all schedules involed with these jobs/runs
      var jobIds = jobRuns.Select(j => j.Job.Id).ToList();
      var scheds = session.EntitySet<IJobSchedule>().Where(js => jobIds.Contains(js.Job.Id)).ToList();
      if(scheds.Count == 0)
        return; 
      foreach(var sched in scheds) {
        var nextStartOn = sched.GetNextStartAfter(utcNow);
        if(nextStartOn != null) {
          var nextRun = NewJobRun(sched.Job, JobRunType.Schedule, nextStartOn);
          sched.NextRunId = nextRun.Id;
        } else {
          sched.NextRunId = null;
          sched.Status = JobScheduleStatus.Stopped;
        }
      } //foreach
    } //method

    private JobRunContext StartJobRun(IJobRun jobRun) {
      Interlocked.Increment(ref _activitiesCounter);
      //create job context 
      var jobCtx = new JobRunContext(jobRun);
      RegisterJobRun(jobCtx);
      OnJobNotify(jobCtx, JobNotificationType.Starting);
      jobCtx.StartInfo = CreateJobStartInfo(jobRun, jobCtx);
      if(jobCtx.StartInfo.ThreadType == JobThreadType.Background) {
        jobCtx.Thread = new Thread(StartBackgroundJobRun);
        jobCtx.Thread.Start(jobCtx);
      } else {
        Task.Run(() => StartPoolJobRunAsync(jobCtx));
      }
      return jobCtx; 
    }

    private void StartBackgroundJobRun(object data) {
      Interlocked.Decrement(ref _activitiesCounter); 
      var jobCtx = (JobRunContext)data;
      try {
        var startInfo = jobCtx.StartInfo;
        startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments);
        OnJobRunFinished(jobCtx);
      } catch(Exception ex) {
        OnJobRunFinished(jobCtx, ex);
        //do not rethrow exc here
      }
    }

    private async Task StartPoolJobRunAsync(object objJobContext) {
      JobRunContext jobCtx = (JobRunContext)objJobContext;
      try {
        var startInfo = jobCtx.StartInfo;
        bool startAsync = startInfo.Method.ReturnType.IsAssignableFrom(typeof(Task));
        if(startAsync) {
          var task = Task.Factory.StartNew(async () => await (Task)startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments)).Unwrap();
          var task2 = task.ContinueWith((t) => OnAsyncTaskCompleted(t, jobCtx));
          Interlocked.Decrement(ref _activitiesCounter);
          await task2;
        } else {
          Interlocked.Decrement(ref _activitiesCounter);
          startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments);
          OnJobRunFinished(jobCtx);
        }
      } catch(Exception ex) {
        OnJobRunFinished(jobCtx, ex);
        //do not rethrow exc here
      }
    }

    private Task OnAsyncTaskCompleted(Task mainTask, JobRunContext jobContext) {
      var realExc = mainTask.Exception == null ? null : GetInnerMostExc(mainTask.Exception); 
      OnJobRunFinished(jobContext, realExc);
      return Task.CompletedTask;
    }

    private void OnJobRunFinished(JobRunContext jobContext, Exception exception = null) {
      UpdateFinishedJobRun(jobContext, exception);
      UnregisterJobRun(jobContext);
      var notifType = exception == null ? JobNotificationType.Completed : JobNotificationType.Error;
      OnJobNotify(jobContext, notifType, exception);
    }

  }
}
