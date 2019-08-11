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
      var items = _queue.ProduceBatch(fireEvent: false);
      if (items.Count > 0)
        SaveBatch(items); 
    }

    private void SaveBatch(IList<LogEntry> items) {
      if (items.Count == 0)
        return;
      var session = _app.OpenSystemSession();


      session.SaveChanges(); 
    }
  }
}
