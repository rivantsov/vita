using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vita.Entities.Logging {
  /// <summary>Container for multiple entries. Used by operation log to group together multiple entries</summary>
  public class BatchedLogEntry : LogEntry {
    public IList<LogEntry> Entries; 

    public BatchedLogEntry(LogContext context, IList<LogEntry> entries) : base(context) {
      Entries = entries; 
    }

    public override string AsText() {
      if(Entries == null || Entries.Count == 0)
        return string.Empty;
      var list = Entries.Select(e => e.AsText()).ToList();
      list.Add(string.Empty); //NewLine will be added before that empty element
      return string.Join(Environment.NewLine, list); 
    }

    public override string ToString() {
      return AsText();
    }
  }
}
