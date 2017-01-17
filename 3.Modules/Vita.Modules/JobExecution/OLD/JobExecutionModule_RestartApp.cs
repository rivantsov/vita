using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Data; 

namespace Vita.Modules.JobExecution {
  public partial class JobExecutionModule {

    private void RestartJobRunsAfterRestart() {
      var utcNow = App.TimeService.UtcNow;
      var ctxList = GetContextsForConnectedDataSources(); 
      foreach(var ctx in ctxList)
        RestartJobRunsAfterRestart(ctx, utcNow);
    }

    private void RestartJobRunsAfterRestart(OperationContext context, DateTime utcNow) {
      // Find failed jobs to start at this time
      var session = context.OpenSystemSession();
        var jobRuns = session.EntitySet<IJobRun>().Include(jr => jr.Job)
          .Where(jr => jr.Status == JobRunStatus.Executing || jr.Status == JobRunStatus.Interrupted)
          .ToList();
        if(jobRuns.Count == 0)
          return;
        //update job run start time, status
        var ids = jobRuns.Select(jr => jr.Id).ToArray();
        var updateQuery = session.EntitySet<IJobRun>().Where(jr => ids.Contains(jr.Id))
                 .Select(jr => new { Status = JobRunStatus.Executing, RunCount = jr.RunCount + 1, LastStartedOn = utcNow });
        updateQuery.ExecuteUpdate<IJobRun>();
        foreach(var jobRun in jobRuns)
          StartJobRun(jobRun);
    }

  }//class
} //ns
