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
  internal static class JobUtil {
    internal static int GetWaitInterval(string intervals, int attemptNumber) {
      Util.Check(attemptNumber >= 2, "AttemptNumber may not be less than 2, cannot retrieve Wait interval.");
      if(string.IsNullOrEmpty(intervals))
        return -1;
      // Attempt number is 1-based; so for attempt 2 the wait time will be the first in the list - 0
      var index = attemptNumber - 2;
      var arr = intervals.Split(',');
      if(index >= arr.Length)
        return -1;
      int result;
      if(int.TryParse(arr[index], out result))
        return result;
      return -1; 
    }

    internal static Exception GetInnerMostExc(Exception ex) {
      if(ex == null)
        return null;
      var aggrEx = ex as AggregateException;
      if(aggrEx == null)
        return ex;
      return aggrEx.Flatten().InnerExceptions[0]; 
    }

  } //class
}
