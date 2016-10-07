using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Web;

namespace Vita.Entities.Services {
  // We are defining it here so it can be used by WebCallContextHandler in Vita.Web and implemented in WebCallModule in Vita.Modules
  public interface IWebCallLogService {
    void Log(WebCallContext webContext);
  }

}
