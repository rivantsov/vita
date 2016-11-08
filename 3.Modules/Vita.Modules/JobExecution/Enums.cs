using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.JobExecution {

  public enum JobRunStatus {
    Executing,
    Completed,
    Failed, // failed, but will continue to retry
    Error,  //failed completely 
    Interrupted, // interrupted by system shutdown
  }

  [Flags]
  public enum JobFlags {
    None = 0,
    StartOnSave = 1 << 1,
    PersistArguments = 1 << 2,
    HasChildJobs = 1 << 3,
    // Light means initially not persisted, trying to execute it on the fly; if failed, it is persisted and retried
    IsLightJob = 1 << 4, 

    Default = StartOnSave
  }

  public enum ThreadType {
    Pool,
    Background,
  }

  public enum JobNotificationType {
    Starting,
    Error,
    Completed,
  }

}
