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

    private void RestartJobRunsDueForRetry() {
      var utcNow = App.TimeService.UtcNow;
      var ctxList = GetContextsForConnectedDataSources();
      foreach(var ctx in ctxList)
        RestartJobRunsDueForRetry(ctx, utcNow);
    }

    private void RestartJobRunsDueForRetry(OperationContext context, DateTime utcNow) {
      const int BatchSize = 100;
      while(true) {
        var session = context.OpenSystemSession();
        var jobRuns = session.EntitySet<IJobRun>().Include(jr => jr.Job)
          .Where(jr => jr.Status == JobRunStatus.Failed && jr.NextStartOn != null && jr.NextStartOn <= utcNow)
          .Take(BatchSize)
          .ToList();
        if(jobRuns.Count == 0)
          return;
        //  remove jobs that are already running - just in case
        var runningJobIds = GetRunningJobIds();
        jobRuns = jobRuns.Where(jr => !runningJobIds.Contains(jr.Job.Id)).ToList();
        //update job run statuses to executing
        var ids = jobRuns.Select(jr => jr.Id).ToArray();
        var updateQuery = session.EntitySet<IJobRun>().Where(jr => ids.Contains(jr.Id))
                 .Select(jr => new {
                   Status = JobRunStatus.Executing, LastStartedOn = utcNow, RunCount = jr.RunCount + 1
                 });
        updateQuery.ExecuteUpdate<IJobRun>();
        foreach(var jobRun in jobRuns)
          StartJobRun(jobRun);
        // If we got the last record, exit
        if(jobRuns.Count < BatchSize)
          return; 
      } //while
    } // method

  }//class
} //ns
