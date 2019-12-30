using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging {

  class LogPersistenceService: ILogPersistenceService, IEntityServiceBase {
    int _batchSize;
    TimerInterval _timerInterval;
    EntityApp _app; 
    ActiveBatchingBuffer<LogEntry> _buffer;
    List<ILogBatchListener> _handlers = new List<ILogBatchListener>(); 

    public LogPersistenceService(int batchSize = 1000, TimerInterval timerInterval = TimerInterval.T_500_Ms) {
      _batchSize = batchSize;
      _timerInterval = timerInterval;
    }

    public void Init(EntityApp app) {
      _app = app; 
      var timers = app.GetService<ITimerService>();
      _buffer = new ActiveBatchingBuffer<LogEntry>(timers, _batchSize, _timerInterval);
      _buffer.Subscribe(OnBatchCreated);
    }

    public void Shutdown() {
    }

    public void Push(LogEntry entry) {
      _buffer.Push(entry);
    }
    public void Flush() {
      _buffer.Flush(); 
    }
    public void RegisterHandler(ILogBatchListener handler) {
      _handlers.Add(handler);
    }

    private void OnCompletedSignal() {
      _buffer.Flush();
    }

    private void OnBatchCreated(IList<LogEntry> entries) {
      var batch = new LogEntryBatch() { Entries = entries };
      batch.Session = _app.OpenSystemSession();
      foreach(var handler in _handlers)
        handler.SaveBatch(batch);
      batch.Session.SaveChanges(); 
    }

  }
}
