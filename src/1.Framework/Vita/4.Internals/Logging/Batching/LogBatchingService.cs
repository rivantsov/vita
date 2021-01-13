using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  public class LogBatchingService : Observable<IList<LogEntry>>, ILogBatchingService, IEntityServiceBase {
    ActiveBatchingBuffer<LogEntry> _buffer;
    int _batchSize;
    TimerInterval _interval;
    bool _initialized; 

    public LogBatchingService(int? batchSize = null, TimerInterval? timerInterval = null) {
      _batchSize = batchSize.HasValue ? batchSize.Value : LogStaticConfig.BatchSize;
      _interval = timerInterval.HasValue ? timerInterval.Value : LogStaticConfig.BatchingTimerInterval; 
    }

    public void Init(EntityApp app) {
      if (_initialized)
        return; //prevent double initialization
      var timers = app.GetService<ITimerService>();
      // create buffer and hook it
      _buffer = new ActiveBatchingBuffer<LogEntry>(timers, _batchSize, _interval);
      var logService = app.GetService<ILogService>();
      logService.Subscribe(_buffer); // logService -> _buffer
      _buffer.Subscribe(Buffer_OnBatchProduced); // _buffer produces batches
      _initialized = true; 
    }

    public void Shutdown() {
      _buffer?.Flush(); 
    }

    private void Buffer_OnBatchProduced(IList<LogEntry> entries) {
      Broadcast(entries); 
    }

    public void OnCompleted() {
      _buffer.Flush();
      base.ForEachSubscription(s => s.OnCompleted());
    }

    public void OnError(Exception error) {      
    }

    public void OnNext(LogEntry value) {
      _buffer.Push(value); 
    }

    public static ILogBatchingService GetCreateLogBatchingService(EntityApp app) {
      var iServ = (ILogBatchingService) app.GetService(typeof(ILogBatchingService));
      if (iServ != null)
        return iServ; 
      // create and register it
      var batchingServ = new LogBatchingService();
      app.RegisterService<ILogBatchingService>(batchingServ);
      batchingServ.Init(app);
      return batchingServ; 
    }

  }
}
