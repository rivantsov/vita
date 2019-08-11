using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Logging {

  public abstract class LogEntry {
    public Guid? Id;
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
  }

}
