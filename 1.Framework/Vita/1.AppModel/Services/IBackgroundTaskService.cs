using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Services {

  /// <summary>Starts background task.</summary>
  /// <remarks>The default implementation is trivial - calls Task.Run(workItem). This works in desktop/console apps. 
  /// But under ASP.NET running background task is tricky - ASP.NET runtime kills any leftover threads 
  /// once the Web request completes. So in Web apps it is replaced by WebBackgroundTaskService (see WebHelper.cs)
  /// </remarks>
  public interface IBackgroundTaskService {
    void QueueBackgroundWorkItem(Action workItem);
  }

}
