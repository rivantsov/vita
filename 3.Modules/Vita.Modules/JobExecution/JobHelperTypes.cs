using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.JobExecution {

  [Flags]
  public enum JobFlags {
    None = 0,
    StartOnSave = 1 << 1, 
    NonPoolThread = 1 << 2, 

    Default = StartOnSave 
  }

  public enum JobStatus {
    Pending,
    Executing,
    Completed,
    Error,
  }

  public class JobStartInfo {
    public Type TargetType;
    public MethodInfo Method;
    public object[] Arguments;
    public string[] SerializedArguments;
  }

  public class JobContext {
    public IEntitySession Session;
    public IJob Job; 


  }

}
