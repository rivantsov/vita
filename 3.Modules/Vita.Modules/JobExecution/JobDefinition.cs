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
    public JobDefinition ParentJob; 
    internal JobStartInfo StartInfo; 

    //constructor is private, we allow only 
    private JobDefinition(string code, LambdaExpression expression, JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null,
          ThreadType threadType = ThreadType.Pool, JobDefinition parentJob = null) {
      Id = Guid.NewGuid(); 
      Code = code;
      Expression = expression;
      Flags = flags;
      RetryPolicy = retryPolicy ?? JobRetryPolicy.Default;
      ThreadType = threadType;
      ParentJob = parentJob;
      if(ParentJob != null) {
        Util.Check(!Flags.IsSet(JobFlags.StartOnSave), 
              "Invalid job definition: the flag StartOnSave may not be set on a job with a parent job. Job code: {0}", code);
        ParentJob.Flags |= JobFlags.HasChildJobs;
      }
      StartInfo = JobUtil.GetJobStartInfo(expression);
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

    public static JobDefinition CreatePoolJob(string code, Expression<Func<JobRunContext, Task>> expression, JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null,
          JobDefinition parentJob = null) {
        return new JobDefinition(code, expression, flags, retryPolicy, ThreadType.Pool, parentJob);
    }

    public static JobDefinition CreateBackgroundJob(string code, Expression<Action<JobRunContext>> expression, JobFlags flags = JobFlags.Default, JobRetryPolicy retryPolicy = null,
          JobDefinition parentJob = null) {
        return new JobDefinition(code, expression, flags, retryPolicy, ThreadType.Background, parentJob);
    }

  }

}//ns
