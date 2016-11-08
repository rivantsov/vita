using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  /// <summary>Defines job retry policy.</summary>
  public class JobRetryPolicy {
    public int IntervalSeconds = 60;
    public int RetryCount = 3;
    public int PauseMinutes = 6 * 60; //3 hours
    public int RoundsCount = 4;

    public static JobRetryPolicy Default = new JobRetryPolicy();
    public static JobRetryPolicy LightJobDefault = new JobRetryPolicy(intervalSeconds: 10, retryCount: 3, pauseMinutes: 30, roundsCount: 5);

    public JobRetryPolicy() { } 
    public JobRetryPolicy(int intervalSeconds, int retryCount, int pauseMinutes, int roundsCount) {
      IntervalSeconds = intervalSeconds;
      RetryCount = retryCount;
      PauseMinutes = pauseMinutes;
      RoundsCount = roundsCount; 
    }
  }
  
  /// <summary>Defines a reliabled job parameters. </summary>
  public class JobDefinition {
    public Guid Id { get; internal set; }
    public string Code;
    internal Expression<Func<JobRunContext, Task>> Expression;
    public JobFlags Flags; 
    public ThreadType ThreadType;
    public JobRetryPolicy RetryPolicy;
    public JobDefinition ParentJob; 
    internal JobStartInfo StartInfo; 


    public JobDefinition(string code, Expression<Func<JobRunContext, Task>> expression, JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null,
          ThreadType threadType = ThreadType.Pool, JobDefinition parentJob = null) {
      Id = Guid.NewGuid(); 
      Code = code;
      Expression = expression;
      Flags = flags;
      RetryPolicy = retryPolicy ?? JobRetryPolicy.Default;
      ThreadType = threadType;
      ParentJob = parentJob; 
      StartInfo = JobUtil.GetJobStartInfo(expression);
      var returnType = StartInfo.Method.ReturnType; 
      if (ThreadType == ThreadType.Background && typeof(Task).IsAssignableFrom(returnType)) {
        Util.Check(false, "Background (non-pool thread) job must not return Task; it must be synchronous.");
      }
    }//constructor
  }

}//ns
