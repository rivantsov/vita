﻿using System;
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
    BatchingQueue<T> _queue = new BatchingQueue<T>();
    object _flushLock = new object();
    bool _flushedSinceLastTimer; 
    bool _flushing; //indicates that flush was requested or is in progress

    enum FlushTrigger {
      Timer,
      Count,
      Code,
    }

    public ActiveBatchingBuffer(ITimerService timerService, int batchSize, TimerInterval flushInterval = TimerInterval.T_500_Ms) {
      _timerService = timerService;
      _batchSize = batchSize;
      _timerService?.Subscribe(flushInterval, OnFlushTimerElapsed);
    }

    private void OnFlushTimerElapsed() {
      // We skip flushing if there was another flush (by batch size) since last timer signal 
      // - to avoid flushing small number of items, leftove== rs from flush by reaching batch size
      if(_queue.Count > 0 && !_flushedSinceLastTimer)
        FlushImpl(FlushTrigger.Timer);
      _flushedSinceLastTimer = false; 
    }

    public int Count => _queue.Count;

    public void Push(T item) {
      var count = _queue.Enqueue(item);
      if(count >= _batchSize && !_flushing) {
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
