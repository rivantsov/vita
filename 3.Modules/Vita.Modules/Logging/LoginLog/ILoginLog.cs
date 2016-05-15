using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Modules.Login;

namespace Vita.Modules.Logging {
  public interface ILoginLogService {
    void LogEvent(OperationContext context, LoginEventType eventType, ILogin login = null, string notes = null, string userName = null);
    ILoginLog GetLastEvent(Guid loginId, LoginEventType eventType);
  }


}
