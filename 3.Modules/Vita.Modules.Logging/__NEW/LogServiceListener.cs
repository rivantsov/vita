using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging {

  public class LogServiceListener {
    LoggingEntityApp _app;
    BatchingQueue<LogEntry> _queue;
    LogSaveService _saveService; 

    public LogServiceListener(LoggingEntityApp app) {
      _app = app;
      _queue = new BatchingQueue<LogEntry>();
      _queue.Batched += Queue_Batched;
      _saveService = new LogSaveService(_app); 
    }

    private void Queue_Batched(object sender, QueueBatchEventArgs<LogEntry> e) {
      Task.Run(() => _saveService.SaveEntries(e.Items)); // default mode, save on background thread
    }

    public void AddEntry(LogEntry entry) {
      _queue.Enqueue(entry); 
    }

    public void Flush() {
      // do not fire event - it will result in saving on background thread; we save items directly
      var items = _queue.ProduceBatch(fireEvent: false);
      if (items.Count > 0)
        _saveService.SaveEntries(items); 
    }

  }

}
