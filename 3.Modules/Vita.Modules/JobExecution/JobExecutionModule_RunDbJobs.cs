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
        var jobRuns = session.EntitySet<IJobRun>().Include(jr => jr.Job)
          .Where(jr => jr.Status == JobRunStatus.Pending && jr.StartOn <= utcNow && 
                 jr.Job.HostName == _settings.HostName)
          .Take(BatchSize)
          .ToList();
        if(jobRuns.Count == 0)
          return;
        //update job run statuses to executing
        var ids = jobRuns.Select(jr => jr.Id).ToArray();
        var updateQuery = session.EntitySet<IJobRun>().Where(jr => ids.Contains(jr.Id))
                 .Select(jr => new {
                   Status = JobRunStatus.Executing, StartedOn = utcNow
                 });
        updateQuery.ExecuteUpdate<IJobRun>();
        foreach(var jobRun in jobRuns)
          StartJobRun(jobRun);
        // If we got the last record, exit
        if(jobRuns.Count < BatchSize)
          return;
      } //while
    } // method

    private JobRunContext StartJobRun(IJobRun jobRun) {
      //create job context 
      var jobCtx = new JobRunContext(jobRun);
      RegisterRunningJob(jobCtx);
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
          await task2;
        } else {
          startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments);
          OnJobRunFinished(jobCtx);
        }
      } catch(Exception ex) {
        OnJobRunFinished(jobCtx, ex);
        //do not rethrow exc here
      }
    }

    private Task OnAsyncTaskCompleted(Task mainTask, JobRunContext jobContext) {
      var realExc = mainTask.Exception == null ? null : JobUtil.GetInnerMostExc(mainTask.Exception); 
      OnJobRunFinished(jobContext, realExc);
      return Task.CompletedTask;
    }

    private void OnJobRunFinished(JobRunContext jobContext, Exception exception = null) {
      UpdateFinishedJobRun(jobContext, exception);
      var notifType = exception == null ? JobNotificationType.Completed : JobNotificationType.Error;
      OnJobNotify(jobContext, notifType, exception);
    }

  }
}
