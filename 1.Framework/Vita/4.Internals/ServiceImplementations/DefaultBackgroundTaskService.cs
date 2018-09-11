using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Services.Implementations {
  // Trivial service; but under ASP.NET running background task is tricky (ASP.NET runtime kills any leftover threads 
  // once Web request completes), so in Web apps it is replaced by WebBackgroundTaskService (see WebHelper.cs)
  public class DefaultBackgroundTaskService : IBackgroundTaskService {
    public void QueueBackgroundWorkItem(Action workItem) {
      Task.Run(workItem);
    }
  }
}
