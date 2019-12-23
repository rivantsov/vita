using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  /// <summary>
  ///   Provides buffering of input elements, and produces batches based on size limit, or triggered by timer
  /// </summary>
  /// <typeparam name="T">Item type.</typeparam>
  /// <remarks>Active in name refers to ability to automatically trigger batch creation; the batch is broadcasted through Observer pattern implementation.</remarks>
  public class ActiveBatchingBuffer<T>: Observable<IList<T>>, IObserver<T> {
    ITimerService _timerService;
    int _batchSize;
    BatchingQueue<T> _entries = new BatchingQueue<T>();
    object _flushLock = new object();
    bool _lastFlushWasByTimer = true; // just initial value
    bool _flushing; //indicates that flush was requested or is in progress

    public ActiveBatchingBuffer(ITimerService timerService, int batchSize, TimerInterval flushInterval = TimerInterval.T_500_Ms) {
      _timerService = timerService;
      _batchSize = batchSize;
      _timerService.Subscribe(flushInterval, OnFlushTimerElapsed);
    }

    public int Count => _entries.Count; 

    public void OnFlushTimerElapsed() {
      // We skip flushing if there was another flush (by batch size) since last timer signal 
      // - to avoid flushing small number of items, leftovers from flush by reaching batch size
      if(_entries.Count > 0 && _lastFlushWasByTimer)
        Flush();
      _lastFlushWasByTimer = true; 
    }

    private void Flush(bool forceAll = false) {
      try {
        // protect against multiple parallel calls to flush
        lock(_flushLock) {
          while(_entries.Count > 0) {
            //do not flush less than batch size, unless it is forced; it might also had been just Flushed from another thread. 
            if(_entries.Count < _batchSize && !forceAll)
              return;
            var batch = _entries.DequeueMany(_batchSize);
            Broadcast(batch);
            if(batch.Count < _batchSize / 2)
              return; // this was last batch; this is extra exit to protect from endless loop when items continue to come
          }
        } //lock
      } finally {
        _lastFlushWasByTimer = false; // timer handler will set this value to true  (if called by timer)
        _flushing = false; 
      }
    } //method

    // IObserver implementation
    public void OnNext(T value) {
      var count = _entries.Enqueue(value);
      if(count >= _batchSize && !_flushing) {
        _flushing = true; 
        Task.Run(() => Flush());
      }
    }
    public void OnCompleted() {
      Flush(forceAll: true); 
    }
    public void OnError(Exception error) {
    }

  } //class

}
