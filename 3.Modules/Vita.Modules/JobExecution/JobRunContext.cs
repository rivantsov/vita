using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  public class JobRunContext {
    public readonly OperationContext OperationContext;
    public readonly DateTime StartedOn;
    public readonly string JobCode;
    public readonly Guid? SourceId; 
    public readonly Guid JobRunId;
    public readonly Guid JobId;
    public readonly JobFlags Flags; 
    public JobRunStatus Status { get; internal set; }
    public bool IsPersisted { get; internal set; }

    public EntityApp App { get { return OperationContext.App; } }
    public CancellationToken CancellationToken { get { return OperationContext.CancellationToken; } }

    internal JobStartInfo StartInfo;
    internal Thread Thread; //background thread for long-running jobs 
    internal Task Task; // task for short tasks
    internal JsonSerializer Serializer;

    internal JobRunContext(EntityApp app, IJobRun jobRun, JsonSerializer serializer) {
      OperationContext = app.CreateSystemContext();
      StartedOn = app.TimeService.UtcNow; 
      Serializer = serializer;
      JobCode = jobRun.Job.Code;
      SourceId = jobRun.SourceId; 
      JobRunId = jobRun.Id;
      var job = jobRun.Job; 
      JobId = job.Id; 
      Flags = job.Flags; 
      _progress = jobRun.Progress;
      IsPersisted = true; 
    }

    // Used for creating 'light' jobs
    internal JobRunContext(EntityApp app, JsonSerializer serializer, string jobCode, Guid? sourceId) {
      OperationContext = app.CreateSystemContext();
      Serializer = serializer;
      JobCode = jobCode;
      SourceId = sourceId; 
      JobRunId = Guid.NewGuid();
      JobId = Guid.NewGuid();
      Flags = JobFlags.IsLightJob;
      _progress = 0;
      Status = JobRunStatus.Executing;
      IsPersisted = false; 
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
      var updQuery = session.EntitySet<IJobRun>().Where(jr => jr.Id == JobRunId)
          .Select(jr => new { Progress = _progress, ProgressMessage = _progressMessage, Log = jr.Log + log });
      updQuery.ExecuteUpdate<IJobRun>(); 
    }

    public bool CanSaveArguments {
      get { return IsPersisted && Flags.IsSet(JobFlags.PersistArguments); }
    }

    public bool TrySaveArguments() {
      if(!CanSaveArguments) //it is a light task, not persisted yet.
        return false; 
      var serArgs = JobUtil.SerializeArguments(StartInfo.Arguments, Serializer);
      var session = OperationContext.OpenSystemSession();
      var updQuery = session.EntitySet<IJobRun>().Where(jr => jr.Id == JobRunId)
          .Select(jr => new { CurrentArguments = serArgs});
      updQuery.ExecuteUpdate<IJobRun>();
      return true; 
    }

    internal void TryCancel() {
      OperationContext.SignalCancellation(); //this will cancel Task
      if(this.Thread != null)
        Thread.Abort();
      Thread.Yield();
    }

  }//class

  public class JobStartInfo {
    public JobRunStatus Status;
    public Type DeclaringType;
    public Object Object;
    public MethodInfo Method;
    public object[] Arguments;
  }


}
