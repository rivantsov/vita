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

  public class JobDefinition {
    public Guid Id { get; internal set; }
    public string Code;
    internal LambdaExpression Expression;
    public JobFlags Flags; 
    public ThreadType ThreadType;
    public int[] RetryIntervals;
    internal JobStartInfo StartInfo; 

    //constructor is private, we allow only 
    private JobDefinition(string code, LambdaExpression jobMethod, JobFlags flags = JobFlags.Default, ThreadType threadType = ThreadType.Pool, int[] retryIntervals = null) {
      Id = Guid.NewGuid(); 
      Code = code;
      Expression = jobMethod;
      Flags = flags;
      RetryIntervals = retryIntervals ?? JobExecutionModule.DefaultRetryIntervals;
      ThreadType = threadType;
      StartInfo = JobUtil.GetJobStartInfo(jobMethod);
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
                  int[] retryIntervals = null) {
      return new JobDefinition(code, jobMethod, flags, ThreadType.Pool, retryIntervals);
    }

    public static JobDefinition CreateBackgroundJob(string code, Expression<Action<JobRunContext>> jobMethod, JobFlags flags = JobFlags.Default,
                   int[] retryIntervals = null) {
        return new JobDefinition(code, jobMethod, flags, ThreadType.Background, retryIntervals);
    }

  }
}//ns
