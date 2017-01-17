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
    /// <summary>The job run was interrupted, most common cause - system shutdown.</summary>
    Interrupted, // interrupted by system shutdown
    /// <summary>The job run was deactivated (killed) from code.</summary>
    Killed,
  }

  /// <summary>Job flags, indicate various job options.</summary>
  [Flags]
  public enum JobFlags {
    /// <summary>No flags</summary>
    None = 0,
  }

  public enum JobRestartHostMode {
    SameInstance, 
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
