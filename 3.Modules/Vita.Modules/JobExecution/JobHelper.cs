using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  public static class JobHelper {
    /// <summary>Executes light async task (job). The job is persisted and retried only if initial execution fails.</summary>
    /// <param name="context">Operation context.</param>
    /// <param name="func">Function to execute. Must be a call to a static or entity module method.</param>
    /// <param name="code">Job code.</param>
    /// <param name="sourceId">Source ID, for example: calendar event ID.</param>
    /// <returns>JobRunContext object for the executed/executing job.</returns>
    public static async Task<JobRunContext> ExecuteWithRetries(OperationContext context, Expression<Func<JobRunContext, Task>> func, string code = "None", Guid? sourceId = null) {
      var jobService = context.App.GetService<IJobExecutionService>();
      Util.Check(jobService != null, "IJobExecutionService not registered with the entity app; add JobExecutionModule.");
      return await jobService.RunLightTaskAsync(context, func, code, sourceId);
    }

    public static bool IsSet(this JobFlags flags, JobFlags flag) {
      return (flags & flag) != 0;
    }

  }//class 
} //namespace
