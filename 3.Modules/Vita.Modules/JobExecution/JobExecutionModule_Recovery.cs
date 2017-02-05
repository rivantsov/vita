using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  public partial class JobExecutionModule {

    // This method takes care of broken/interrupted job runs that were stopped by previous app shutdown. 
    // Note that we pick-up only job runs assigned to this app server - we match HostName. 
    private int StartInterruptedJobsAfterRestartImpl(OperationContext context) {
      var session = context.OpenSession();
      // Find jobs that are shown as Executing or Interrupted in db but are not actually executing
      // Interrupted are those that were updated by the system in OnShutdown even. 
      // If app crashed unexpectedly and did not have a chance to update statuses, there might be some runs with status Executing
      var jobRuns = session.EntitySet<IJobRun>()
        .Include(jr => jr.Job)
        .Where(jr => jr.HostName == _settings.HostName &&
                (jr.Status == JobRunStatus.Executing || jr.Status == JobRunStatus.Interrupted))
        .ToList();
      if(jobRuns.Count == 0)
        return 0;
      // Get IDs of currently running jobs - just in case...
      var allRunningIds = GetRunningJobs().Select(jc => jc.JobRunId);
      // filter out running jobs; what remains is jobs to restart
      var runsToRestart = jobRuns.Where(jr => !allRunningIds.Contains(jr.Id)).ToList();
      // Create new retry job runs for the same job; update status of old runs to InterruptedRestarted - so that they will not restart if the app shuts down again.
      foreach(var run in runsToRestart) {
        run.Status = JobRunStatus.InterruptedRestarted;
        var newRun = CreateRetryRun(run, 0);
        newRun.Status = JobRunStatus.Executing;
        if(_incidentLog != null) {
          var msg = StringHelper.SafeFormat("Job '{0}' (Job run id: {1}) was scheduled to restart after app shutdown/restart," + 
            " datasource/database: {2}.", run.Job.Name, run.Id, context.DataSourceName);
          _incidentLog.LogIncident("JobRestart", msg, "Restart", newRun.Id, run.Id, run.Job.Name);
        }
      }
      session.SaveChanges();
      // actually start job runs
      foreach(var run in runsToRestart)
        StartJobRun(run);
      return runsToRestart.Count;
    }

    private void CheckLongOverdueJobRuns() {
      var pastTime = App.TimeService.UtcNow.AddMinutes(-30);
      var ctxList = GetContextsForConnectedDataSources();
      foreach(var ctx in ctxList) {
        var session = ctx.OpenSystemSession();
        // 1. Get job runs that are more than 30 minutes overdue, assigned to any app server (host)
        var jobRuns = session.EntitySet<IJobRun>()
          .Include(jr => jr.Job)
          .Where(jr => jr.Status == JobRunStatus.Pending && jr.StartOn <= pastTime)
          .ToList();
        if(jobRuns.Count == 0)
          return;
        //2. Reassign host to this host
        var thisHost = _settings.HostName; 
        var ids = jobRuns.Select(jr => jr.Id).ToArray();
        var updateQuery = session.EntitySet<IJobRun>()
                 .Where(jr => ids.Contains(jr.Id))
                 .Select(jr => new {HostName = thisHost });
        // updateQuery.ExecuteUpdate<IJobRun>();
        updateQuery.ExecuteUpdate<IJobRun>(); 
      }
    }

  }
}
