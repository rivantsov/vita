using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Logging {

  public abstract class LogEntry {
    public Guid? Id;
    public LogEntryType EntryType;
    public DateTime CreatedOn;
    public LogEntryContextInfo ContextInfo;

    public LogEntry(LogEntryType entryType = LogEntryType.Information) {
      EntryType = entryType;
      CreatedOn = TimeService.Instance.UtcNow; 
    }

    public LogEntry(LogEntryContextInfo contextInfo, LogEntryType entryType) : this(entryType) {
      ContextInfo = contextInfo;
    }
    public LogEntry(OperationContext context, LogEntryType entryType) : this(entryType) {
      ContextInfo = new LogEntryContextInfo(context);
    }

    public abstract string AsText();
  }

}
