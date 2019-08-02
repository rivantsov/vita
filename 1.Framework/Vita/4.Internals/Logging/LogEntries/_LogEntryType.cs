using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public enum LogEntryType {
    Information,
    DbCommand,
    Batch,

    Error,
    AppEvent,
    WebCall,
    WebClientCall,
    Transaction,
    Custom
  }

}
