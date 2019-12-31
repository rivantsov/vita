using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  /// <summary>
  ///   Provides buffering of input elements, and produces batches based on size limit, triggered by timer or from external call.
  /// </summary>
  /// <typeparam name="T">Item type.</typeparam>
  /// <remarks>Active in name refers to the ability to automatically trigger batch creation,
  ///   and then broadcasting it through Observable pattern implementation.</remarks>
  public class ActiveBatchingBuffer<T>: Observable<IList<T>>, IObserver<T> {

    ITimerService _timerService;
    int _batchSize;
    //BatchingQueue<T> _queue = new BatchingQueue<T>();
    BatchingQueue<T> _queue = new BatchingQueue<T>();
    object _flushLock = new object();
    bool _flushedSinceLastTimer; 
    bool _flushing; //indicates that flush was requested or is in progress

    public int Count => _queue.Count;

    public ActiveBatchingBuffer(ITimerService timerService, int batchSize, TimerInterval flushInterval = TimerInterval.T_500_Ms) {
      _timerService = timerService;
      _batchSize = batchSize;
      _timerService?.Subscribe(flushInterval, OnFlushTimerElapsed);
    }

    enum FlushTrigger {
      Timer,
      Count,
      Code,
    }

    private void OnFlushTimerElapsed() {
      // We skip flushing if there was another flush (by batch size) since last timer signal 
      // - to avoid flushing small number of items, leftovers from flush by size 
      if(_queue.Count > 0 && !_flushedSinceLastTimer)
        FlushImpl(FlushTrigger.Timer);
      _flushedSinceLastTimer = false; 
    }

    public void Push(T item) {
      _queue.Enqueue(item);
      if(_queue.Count >= _batchSize && !_flushing) {
        _flushing = true;
        Task.Run(() => FlushImpl(FlushTrigger.Count));
      }
    }

    public void Flush() {
      FlushImpl(FlushTrigger.Code);
    }

    private void FlushImpl(FlushTrigger trigger) {
      try {
        // protect against multiple parallel calls to flush
        lock(_flushLock) {
          while(_queue.Count > 0) {
            //do not flush less than batch size, if it is triggered by size; it can happen 
            //  if we have multiple batches in this loop, and at the end a few items left in the queue
            if(_queue.Count < _batchSize && trigger == FlushTrigger.Count)
              return;
            var batch = _queue.DequeueMany(_batchSize);
            Broadcast(batch);
          }
        } //lock
      } finally {
        _flushedSinceLastTimer = true; // timer handler will overwrite it
        _flushing = false; 
      }
    } //method

    // IObserver implementation
    void IObserver<T>.OnNext(T item) {
      Push(item);
    }
    void IObserver<T>.OnCompleted() {
      FlushImpl(FlushTrigger.Code); 
    }
    
    void IObserver<T>.OnError(Exception error) {
    }

  } //class

}
