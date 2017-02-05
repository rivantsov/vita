using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.JobExecution {
  public static class JobExtensions {

    public static bool IsSet(this JobModuleFlags flags, JobModuleFlags flag) {
      return (flags & flag) != 0; 
    }


  } //class
}
