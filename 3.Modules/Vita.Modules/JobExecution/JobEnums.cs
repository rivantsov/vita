using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.JobExecution {

  /// <summary>Represents a status of a job run (execution attempt).</summary>
  public enum JobRunStatus {
    Pending = 0,
    /// <summary>The job is executing.</summary>
    Executing,
    /// <summary>The job run has completed successfully.</summary>
    Completed,
    /// <summary>The job run failed.</summary>
    Error,
    /// <summary>The job run was deactivated (killed) from code.</summary>
    Killed,
    /// <summary>The job run was interrupted, usually by app shutdown.</summary>
    Interrupted,
    /// <summary>The job run was interrupted, but then restarted after app restart.</summary>
    InterruptedRestarted,
  }

  /// <summary>Job run type.</summary>
  public enum JobRunType {
    /// <summary>Immediate job, persisted only on initial failure. </summary>
    Immediate,
    /// <summary>The job run is started on session.SaveChanges().</summary>
    OnSave,
    /// <summary>The job run is a retry attempt.</summary>
    Retry,
    /// <summary>The job run was scheduled to run at certain date-time.</summary>
    ScheduledDateTime,
    /// <summary>The job run was created by CRON schedule.</summary>
    Schedule,
  }

  /// <summary>Defines job execution thread types.</summary>
  public enum JobThreadType {
    /// <summary>Execute on a pool thread, typically as short async Task.</summary>
    Pool,
    /// <summary>Execute on a background, non-pool thread. Use it for long-running tasks.</summary>
    Background,
  }

  /// <summary>Defines job notification types.</summary>
  public enum JobNotificationType {
    /// <summary>The job run is about to start.</summary>
    Starting,
    /// <summary>The job run failed with exception thrown.</summary>
    Error,
    /// <summary>The job run completed successfully.</summary>
    Completed,
  }

}
