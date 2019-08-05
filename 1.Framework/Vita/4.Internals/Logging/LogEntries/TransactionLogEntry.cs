using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  //Temp object used to store trans information in the background update queue
  public class TransactionLogEntry : LogEntry {

    public DateTime StartedOn; 
    public int Duration;
    public int RecordCount;
    public string Changes;
    public long TransactionId;

    public TransactionLogEntry(LogContext context, long transactionId, DateTime startedOn, int duration, int recordCount, string changes)
                     : base(LogEntryType.Transaction, context){
      TransactionId = transactionId;
      StartedOn = startedOn;
      CreatedOn = AppTime.UtcNow;
      Duration = duration;
      RecordCount = recordCount;
      Changes = changes;
    }

    private string _asText;
    public override string AsText() {
      return _asText = _asText ?? $"Transaction: {RecordCount} records, {Duration} ms. User: {Context?.User.UserName} ";
    }

    public override string ToString() {
      return AsText();
    }
  }//class

}
