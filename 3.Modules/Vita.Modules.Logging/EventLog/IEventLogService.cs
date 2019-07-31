using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Vita.Modules.Logging {
  public interface IEventLogService {
    void LogEvents(IList<EventData> events);
  }
}
