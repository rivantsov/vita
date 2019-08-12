using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging {

  public class LogServiceListener : ILogListener {
    LoggingEntityApp _app;
    BatchingQueue<LogEntry> _queue;
    ObjectCache<Guid, UserInfo> UserCache; 

    public LogServiceListener(LoggingEntityApp app) {
      _app = app;
      _queue = new BatchingQueue<LogEntry>();
      _queue.Batched += Queue_Batched;
    }

    private void Queue_Batched(object sender, QueueBatchEventArgs<LogEntry> e) {
      Task.Run(() => SaveBatch(e.Items)); // default mode, save on background thread
    }

    public void AddEntry(LogEntry entry) {
      _queue.Enqueue(entry); 
    }

    public void Flush() {
      // do not fire event - it will result in saving on background thread; we save items directly
      var items = _queue.ProduceBatch(fireEvent: false);
      if (items.Count > 0)
        SaveBatch(items); 
    }

    private void SaveBatch(IList<LogEntry> entries) {
      if (entries.Count == 0)
        return;
      var session = _app.OpenSystemSession();
      foreach(var entry in entries) {
        switch(entry) {

          case InfoLogEntry infoEntry:
            var infoEnt = session.NewLogEntity<IOperationLog>(entry);
            infoEnt.Message = infoEntry.AsText(); 
            break;

          case BatchedLogEntry batchEntry:
            var batchEnt = session.NewLogEntity<IOperationLog>(entry);
            batchEnt.Message = batchEntry.AsText();
            break;

          case ErrorLogEntry errEntry:
            var errEnt = session.NewLogEntity<IErrorLog>(entry);
            errEnt.Message = errEntry.Message;
            errEnt.Details = errEntry.Details;
            errEnt.ExceptionType = errEntry.ExceptionType?.Name;
            break;

          case WebCallLogEntry webEntry:
            var webEnt = session.NewLogEntity<IWebCallLog>(entry);
            webEnt.ControllerName = webEntry.Response?.ControllerName;

            break; 
        }
      }


      session.SaveChanges(); 
    }
  }

  public static class LogSessionExtensions {
    public static TEntity NewLogEntity<TEntity>(this IEntitySession session, LogEntry entry) where TEntity: class, ILogEntityBase {
      var ent = session.NewEntity<TEntity>();
      ent.CreatedOn = entry.CreatedOn;
      ent.Id = entry.Id;
      ent.SessionId = entry.Context.SessionId;
      ent.WebCallId = entry.Context.WebCallId;

      return ent; 
    }
  }
}
