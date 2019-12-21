using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Services.Implementations;
using Vita.Internals.Utilities;
using static Vita.Internals.Utilities.LinkedQueue;

namespace Vita.Entities.Logging {

  public abstract class LogEntry: ILinkedNode {
    public Guid Id;
    public LogEntryType EntryType;
    public DateTime CreatedOn;
    public LogContext Context;

    public LogEntry(LogEntryType entryType) {
      EntryType = entryType;
      CreatedOn = TimeService.Instance.UtcNow; 
    }

    public LogEntry(LogEntryType entryType, LogContext context) : this(entryType) {
      Context = context;
    }

    public abstract string AsText();

    #region ILinkedNode implementation
    // implements ILinkedNode - this allows LogEntry instances to be used in LinkedQueue - slim version of 
    // BufferingQueue. This  avoids creating extra LinkedNode objects inside buffering queue. 
    ILinkedNode ILinkedNode.Next { get; set; }
    #endregion 

  }
}
