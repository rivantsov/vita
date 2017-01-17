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

    private void StartJob(IJob job) {
      //check if job is already running
      var jobCtx = GetRunningJob(job.Id);
      if(jobCtx != null)
        return;
      var session = EntityHelper.GetSession(job);
      var jobRun = job.NewJobRun();
      jobRun.LastStartedOn = session.Context.App.TimeService.UtcNow;
      jobRun.RunCount = 1;
      session.SaveChanges();
      StartJobRun(jobRun);
    }

    private void StartJobRun(IJobRun jobRun) {
      //create job context 
      var jobCtx = new JobRunContext(this.App, jobRun, _serializer);
      RegisterRunningJob(jobCtx);
      OnJobNotify(jobCtx, JobNotificationType.Starting);
      jobCtx.StartInfo = JobUtil.GetJobStartInfo(jobRun, jobCtx);
      if(jobRun.Job.ThreadType == ThreadType.Background) {
        jobCtx.Thread = new Thread(StartBackgroundJobRun);
        jobCtx.Thread.Start(jobCtx);
      } else {
        Task.Run(() => StartPoolJobRunAsync(jobCtx));
      }
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

    private async Task StartPoolJobRunAsync(object data) {
      JobRunContext jobCtx = (JobRunContext)data;
      try {
        var startInfo = jobCtx.StartInfo;
        bool startAsync = startInfo.Method.ReturnType.IsAssignableFrom(typeof(Task));
        if(startAsync) {
          var task = Task.Factory.StartNew(async () => await (Task)startInfo.Method.Invoke(startInfo.Object, startInfo.Arguments)).Unwrap();
          var task2 = task.ContinueWith((t) => OnAsyncJobRunCompleted(t, jobCtx));
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

    private Task OnAsyncJobRunCompleted(Task mainTask, JobRunContext jobContext) {
      var realExc = GetInnerMostExc(mainTask.Exception); // (mainTask.Exception == null) ? null : mainTask.Exception.InnerExceptions[0];
      OnJobRunFinished(jobContext, realExc);
      return Task.CompletedTask;
    }

    private Exception GetInnerMostExc(Exception ex) {
      if(ex == null)
        return null;
      var aggrEx = ex as AggregateException;
      if(aggrEx == null)
        return ex;
      return GetInnerMostExc(aggrEx.InnerExceptions[0]);
    }

    private void OnJobRunFinished(JobRunContext jobContext, Exception exception = null) {
      UpdateFinishedJobRun(jobContext, exception);
      var notifType = exception == null ? JobNotificationType.Completed : JobNotificationType.Error;
      OnJobNotify(jobContext, notifType, exception); 
    }
  }//class
}//ns
