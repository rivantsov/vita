using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  public class JobStartInfo {
    public JobRunStatus Status;
    public Type DeclaringType;
    public Object Object;
    public MethodInfo Method;
    public object[] Arguments;
    public bool ReturnsTask;
    public JobThreadType ThreadType;

    internal JobStartInfo() { }
  }

  /// <summary>Holds information about current job run. </summary>
  [DebuggerDisplay("{JobName}")]
  public class JobRunContext {
    public readonly OperationContext OperationContext;
    public readonly DateTime StartedOn;
    public readonly string JobName;
    public readonly Guid JobId;
    public readonly JobRunType RunType;
    public readonly Guid JobRunId;
    public readonly int AttemptNumber; 
    public JobRunStatus Status { get; internal set; }
    /// <summary>True if the job record had been persisted already. It is usually false for light tasks that start 
    /// without persisting using JobHelper.ExecuteWithRetries method. </summary>
    public bool IsPersisted { get; internal set; }

    /// <summary>A Guid value passed to individual job run when invoked by JobHelper.ScheduleJobRunOn() method.</summary>
    public Guid? DataId;
    /// <summary>A string value passed to individual job run when invoked by JobHelper.ScheduleJobRunOn() method.</summary>
    public string Data; 

    public EntityApp App { get { return OperationContext.App; } }
    public CancellationToken CancellationToken { get { return OperationContext.CancellationToken; } }

    internal JobStartInfo StartInfo;
    //internal Thread Thread; //background thread for long-running jobs 

    internal JobRunContext(IJobRun jobRun) {
      var session = EntityHelper.GetSession(jobRun);
      var app = session.Context.App;
      UserInfo user = (jobRun.UserId == null) ? UserInfo.System : new UserInfo(jobRun.UserId.Value, null);
      OperationContext = new OperationContext(app, user);
      StartedOn = app.TimeService.UtcNow; 
      JobName = jobRun.Job.Name;
      JobRunId = jobRun.Id;
      RunType = jobRun.RunType;
      AttemptNumber = jobRun.AttemptNumber; 
      var job = jobRun.Job; 
      JobId = job.Id; 
      _progress = jobRun.Progress;
      IsPersisted = true;
      DataId = jobRun.DataId;
      Data = jobRun.Data; 
    }

    // Used for creating 'light' jobs
    internal JobRunContext(OperationContext context, string jobName, JobStartInfo startInfo, JobRunType runType) {
      OperationContext = context;
      JobName = jobName ?? "Unnamed/" + JobRunId;
      StartInfo = startInfo;
      StartedOn = context.App.TimeService.UtcNow; 
      JobRunId = Guid.NewGuid();
      JobId = Guid.NewGuid();
      RunType = runType;
      _progress = 0;
      Status = JobRunStatus.Executing;
      IsPersisted = false;
      AttemptNumber = 1; 
    }

    public bool IsCancelled {
      get { return CancellationToken.IsCancellationRequested; }
    }

    public double Progress {
      get { return _progress; }
    } double _progress;

    public string ProgressMessage {
      get { return _progressMessage; }
    } string _progressMessage;


    public void UpdateProgress(double progress, string progressMessage = null) {
      _progress = progress;
      _progressMessage = progressMessage;
      if(!IsPersisted)
        return;
      var log = string.IsNullOrWhiteSpace(progressMessage) ? string.Empty : progressMessage + Environment.NewLine;
      var session = OperationContext.OpenSystemSession();
      // SQL CE does not support string concatenation for 'text' columns, so commenting out Log update
      var updQuery = session.EntitySet<IJobRun>().Where(jr => jr.Id == JobRunId)
          .Select(jr => new { Progress = _progress, ProgressMessage = _progressMessage /*, Log = jr.Log + log */ });
      updQuery.ExecuteUpdate<IJobRun>(); 
    }

    internal void TryCancel() {
      OperationContext.SignalCancellation(); //this will cancel Task
      // if(this.Thread != null)
      //  Thread.Abort();
      Thread.Yield();
    }

  }//class


}
