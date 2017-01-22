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

    internal static IJobRun NewJobRun(this IJob job, DateTime? startOn = null, Guid? dataId = null, string data = null) {
      var session = EntityHelper.GetSession(job);
      var timeService = session.Context.App.TimeService; 
      var jobRun = session.NewEntity<IJobRun>();
      jobRun.Job = job;
      jobRun.StartOn = startOn == null ? timeService.UtcNow : startOn.Value;
      jobRun.DataId = dataId;
      jobRun.Data = data;
      jobRun.AttemptNumber = 1;
      jobRun.UserId = session.Context.User.UserId;
      return jobRun;

    }

    internal static IJobSchedule CreateJobSchedule(this IJob job, string name, string cronSpec, DateTime? activeFrom, DateTime? activeUntil) {
      Util.Check(job.Schedule == null, "Job '{0}' already has a schedule assigned, cannot create another one.", job.Name);
      var session = EntityHelper.GetSession(job);
      var timeService = session.Context.App.TimeService;
      var sched = session.NewEntity<IJobSchedule>();
      sched.Job = job; 
      sched.Name = name; 
      sched.CronSpec = cronSpec;
      sched.ActiveFrom = activeFrom == null ? timeService.UtcNow : activeFrom.Value;
      sched.ActiveUntil = activeUntil;
      return sched;
    }

    internal static void VerifyJobSchedule(this IJobSchedule sched) {
      var session = EntityHelper.GetSession(sched);
      var job = sched.Job;
      var utcNow = session.Context.App.TimeService.UtcNow;
      var nextRunId = sched.NextRunId;
      IJobRun nextRun = (nextRunId == null) ? null : session.GetEntity<IJobRun>(nextRunId.Value);
      switch(sched.Status) {
        case JobScheduleStatus.Stopped:
          // if there is a pending run in the future, kill it
          if(nextRun != null && nextRun.Status == JobRunStatus.Pending && nextRun.StartOn > utcNow.AddMinutes(1))
            nextRun.Status = JobRunStatus.Killed;
          break;
        case JobScheduleStatus.Active:
          // Create or adjust JobRun entity for next run
          var nextStartOn = sched.GetNextStartAfter(utcNow);
          if(nextStartOn != null) {
            if(nextRun == null || nextRun.Status != JobRunStatus.Pending) {
              nextRun = job.NewJobRun(nextStartOn);
              sched.NextRunId = nextRun.Id;
            } else
              nextRun.StartOn = nextStartOn.Value;
          } else 
            //nextSTartOn == null
            sched.NextRunId = null; 
          break;
      }//switch
    }//method

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
