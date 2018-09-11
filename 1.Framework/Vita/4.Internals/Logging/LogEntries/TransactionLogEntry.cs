using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  //Temp object used to store trans information in the background update queue
  public class TransactionLogEntry : LogEntry {
    public Guid? WebCallId;
    public Guid? ProcessId;

    public DateTime StartedOn; 
    public int Duration;
    public int RecordCount;
    public string Changes;

    public TransactionLogEntry(OperationContext context, Guid id, DateTime startedOn, int duration, int recordCount, string changes)
      : base(context, LogEntryType.Transaction){
      Id = id;
      StartedOn = startedOn;
      CreatedOn = context.App.TimeService.UtcNow;
      if(context.WebContext != null)
        WebCallId = context.WebContext.Id;
      ProcessId = context.ProcessId;
      Duration = duration;
      RecordCount = recordCount;
      Changes = changes;
    }

    private string _asText;
    public override string AsText() {
      return _asText = _asText ?? $"Transaction: {RecordCount} records, {Duration} ms. User: {ContextInfo?.UserName} ";
    }

    public override string ToString() {
      return AsText();
    }
  }//class

}
