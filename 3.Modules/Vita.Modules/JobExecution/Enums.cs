using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.JobExecution {

  /// <summary>Represents a status of a job run (execution attempt).</summary>
  public enum JobRunStatus {
    /// <summary>The job is executing.</summary>
    Executing = 0,
    /// <summary>The job run has completed successfully.</summary>
    Completed,
    /// <summary>The job run failed, but will be retried in the future.</summary>
    Failed, // failed, but will continue to retry
    /// <summary>The job runs failed after exausting all retry attempts.</summary>
    Error,  //failed completely 
    /// <summary>The job run was interrupted, most common cause - system shutdown.</summary>
    Interrupted, // interrupted by system shutdown
  }

  /// <summary>Job flags, indicate various job options.</summary>
  [Flags]
  public enum JobFlags {
    /// <summary>No flags</summary>
    None = 0,
    /// <summary>Start the job run immediately after job entity is saved to the database.</summary>
    StartOnSave = 1 << 1,
    /// <summary>Indicates that job method arguments must be serialized back to database on intermittent failures.</summary>
    PersistArguments = 1 << 2,
    // Light means initially not persisted, trying to execute it on the fly; if failed, it is persisted and retried
    /// <summary>The job is not persisted initially but is tried to execute immediately. If fails, it is persisted to be retried later.</summary>
    IsLightJob = 1 << 4,

    /// <summary>Default flags: StartOnSave.</summary>
    Default = StartOnSave
  }

  /// <summary>Defines job execution thread types.</summary>
  public enum ThreadType {
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
