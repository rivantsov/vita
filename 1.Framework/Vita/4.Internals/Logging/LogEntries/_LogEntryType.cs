using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public enum LogEntryType {
    Information,
    //Command,
    //Composite,

    Error,
    Event,
    WebCall,
    WebClientCall,
    Transaction,
    Custom
  }

}
