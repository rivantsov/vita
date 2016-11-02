using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.JobExecution {
  public interface IJobExecutionService {
    Guid CreateJob(IEntitySession session, string code, Expression<Action> lambda, JobFlags flags, int retryCount = 5, int retryIntervalMinutes = 5, Guid? ownerId = null);
  }
}
