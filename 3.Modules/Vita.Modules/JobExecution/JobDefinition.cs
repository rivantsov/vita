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
    public int PauseMinutes = 30; 
    public int RoundsCount = 4;

    public static JobRetryPolicy Default = new JobRetryPolicy();
    public static JobRetryPolicy DefaultLightTask = new JobRetryPolicy(intervalSeconds: 10, retryCount: 3, pauseMinutes: 30, roundsCount: 5);

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
    internal LambdaExpression Expression;
    public JobFlags Flags; 
    public ThreadType ThreadType;
    public JobRetryPolicy RetryPolicy;
    internal JobStartInfo StartInfo; 

    //constructor is private, we allow only 
    private JobDefinition(string code, LambdaExpression jobMethod, JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null,
          ThreadType threadType = ThreadType.Pool) {
      Id = Guid.NewGuid(); 
      Code = code;
      Expression = jobMethod;
      Flags = flags;
      RetryPolicy = retryPolicy ?? JobRetryPolicy.Default;
      ThreadType = threadType;
      StartInfo = JobExtensions.GetJobStartInfo(jobMethod);
      var returnType = StartInfo.Method.ReturnType;
      switch(ThreadType) {
        case ThreadType.Pool:
          Util.Check(returnType == typeof(Task), "Async pool job method must return Task. Job code: {0}.", code);
          break;
        case ThreadType.Background:
          Util.Check(returnType == typeof(void), "Background job method must be void. Job code: {0}.", code);
          break;
      }
    }

    public static JobDefinition CreatePoolJob(string code, Expression<Func<JobRunContext, Task>> jobMethod, JobFlags flags = JobFlags.Default,
                  JobRetryPolicy retryPolicy = null) {
      return new JobDefinition(code, jobMethod, flags, retryPolicy, ThreadType.Pool);
    }

    public static JobDefinition CreateBackgroundJob(string code, Expression<Action<JobRunContext>> jobMethod, JobFlags flags = JobFlags.Default,
                   JobRetryPolicy retryPolicy = null) {
        return new JobDefinition(code, jobMethod, flags, retryPolicy, ThreadType.Background);
    }

  }

}//ns
