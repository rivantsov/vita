using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Web {
  // Under ASP.NET running long-running task is a challenge: 
  // traditional methods Task.Run(...), ThreadPool or separate thread does not work - 
  // ASP.NET terminates all jobs/threads related to the request when the request is completed. 
  // We have to go through HostingEnvironment.QueueBackgroundWorkItem method - provided specifically
  // for this purpose. 
  // There is one problem - running unit tests that use Self-hosted web app. In this case this does
  // not work. We use workaround - invoking action thru the timer. 
  class WebBackgroundTaskService : IBackgroundTaskService {
    EntityApp _app;

    public WebBackgroundTaskService(EntityApp app) {
      _app = app; 
    }

    public void QueueBackgroundWorkItem(Action action) {
      if(HostingEnvironment.IsHosted) {
        HostingEnvironment.QueueBackgroundWorkItem((tkn) => action());
        return;
      }
      // The case for test environment with self-hosting 
      var timer = new Timer(TimerAction, action, 0, Timeout.Infinite);
    }
    private void TimerAction(object state) {
      var action = (Action)state;
      Task.Run(action); 
    }

  }
}
