using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {
  public class MessageLogEntry: LogEntry {
    public string Category;
    public string SubType;
    public string Recipient;
    public string RecipientAddress;
    public string From;
    public string FromAddress; 
    public string Subject;
    public string Body;
    public string Tags; 

    public MessageLogEntry(LogContext context) : base(LogEntryType.Message, context) {

    }

    public override string AsText() {
      return $"To:{Recipient}, Subject:{Subject}";
    }
  }
}
