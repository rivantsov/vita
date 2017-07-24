using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Services {
  public interface IBackgroundTaskService {
    void QueueBackgroundWorkItem(Action workItem); 
  }
}
