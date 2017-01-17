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

    private void ProcessComingScheduledEvents(IEntitySession session, DateTime utcNow) {
      int BatchSize = 100;
      while(true) {
        //Find and process due schedules in batches of 100 
        var schedules = session.EntitySet<IJobSchedule>()
            .Where(es => es.Status == JobScheduleStatus.Active &&
                // we are picking up all past events that are still active
                es.NextStartOn != null && es.NextStartOn < utcNow)
                .Take(BatchSize)
                .ToList();
        if(schedules.Count == 0)
          return;
        foreach(var sched in schedules) {
          var jobRun = sched.Job.NewJobRun();
        }
        session.SaveChanges();
        if(schedules.Count < BatchSize)
          return; //there are no more
      }//while
    }


  }//class
}
