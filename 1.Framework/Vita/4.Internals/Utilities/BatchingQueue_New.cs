using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Utilities {

  /// <summary>Encodes the source event that triggered the flush - producing a batch from the queue. </summary>
  public enum BatchTrigger {
    /// <summary>The batch was produced when number of queued items reached the specified limit.</summary>
    Size,

    /// <summary>The batch was produced by the timer event.</summary>
    Timer,

    /// <summary>The batch was triggered by the OnCompleted call from the pipeline.</summary>
    Completed,

    /// <summary>The batch was triggered by an explicit call from the external code.</summary>
    Code,
  }

  /// <summary>A component responsible for initial batching of the input items. </summary>
  /// <typeparam name="T">The input data type.</typeparam>
  /// <remarks>
  ///     The active queue produces batches either based on size (when number of accumulated items
  ///     reaches the specified BatchSize limit), or by timer (thus ensuring that items do not stay in the queue
  ///     for too long).
  /// </remarks>
  public class ActiveBatchingQueue<T> : Observable<Batch<T>>, IObserver<T> {
    /// <summary>Validation constant, specifies minimum flushing interval allowed. </summary>
    public const int MinLingerIntervalMs = 50;

    class BatchBuffer {
      public int ItemCount;
      public IList<T> FlushedItems;
      public DateTime FirstReceivedAt = DateTime.MinValue;
      public DateTime LastReceivedAt;

      private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

      public int Enqueue(T item) {
        _queue.Enqueue(item);
        var newCount = Interlocked.Increment(ref ItemCount); //returns incremented value
        LastReceivedAt = AppTime.UtcNow;
        if (FirstReceivedAt == DateTime.MinValue) {
          FirstReceivedAt = LastReceivedAt;
        }

        FlushedItems = null;
        return newCount;
      }

      public IList<T> GetAll() {
        return _queue.ToArray();
      }
    }


    public int Length => _buffer.ItemCount;

    public int QueuedBatchCount => _queuedBatchCount;

    private readonly UploaderConfig _config;
    private BatchBuffer _buffer;
    private readonly System.Timers.Timer _flushTimer;
    private int _queuedBatchCount;
    private DateTime _lastFlushedOn;
    private readonly object _flushLock = new object();
    private bool _inShutdown;

    /// <summary>Constructs a new instance. </summary>
    /// <param name="config">Uploader config.</param>
    public ActiveBatchingQueue(UploaderConfig config) {
      _config = config;
      if (_config.MaxItemLingerTime != null) {
        var lingerMs = _config.MaxItemLingerTime.Value.TotalMilliseconds;
        ThrowIf.True(lingerMs < MinLingerIntervalMs, nameof(UploaderConfig.MaxItemLingerTime),
            $"Batching queue max linger time may not be less than {MinLingerIntervalMs} ms.");
        // we have to run timer at half linger interval to guarantee the linger time. 
        _flushTimer = new System.Timers.Timer(lingerMs / 2);
        _flushTimer.Elapsed += Timer_Elapsed;
        _flushTimer.Start();
      }

      _buffer = new BatchBuffer();
    }


    /// <summary>Pushes the next data into the queue. </summary>
    /// <param name="item">The input data.</param>
    public void OnNext(T item) {
      // no-batching mode (ex: ARIS alerts)
      if (_config.BatchSize == 1) {
        BroadcastSingleItem(item);
        return;
      }

      var oldBuffer = _buffer;
      var count = oldBuffer.Enqueue(item);
      // check buffer size and flush if reached target size
      if (count >= _config.BatchSize && oldBuffer == _buffer) {
        Flush(oldBuffer, BatchTrigger.Size);
        // FlushedItems is non null; it means buffer was already flushed; check if our item was included
        if (oldBuffer.FlushedItems != null && !oldBuffer.FlushedItems.Contains(item)) {
          // once in a million years event - the buffer was just flushed on parallel thread, but our item did not get into flushed batch
          // - push it into new buffer
          _buffer.Enqueue(item);
        }
      }
    }

    internal void Flush(BatchTrigger trigger) {
      Flush(_buffer, trigger);
    }

    private void Flush(BatchBuffer buffer, BatchTrigger trigger) {
      lock (_flushLock) {
        if (buffer != _buffer) {
          return; //already flushed on another thread
        }

        // replace old buffer with new buffer, and send out items from old buffer on background thread
        _buffer = new BatchBuffer();

        // process buffer and broadcast batch on background thread; keep exact count of batches in progress - count decrements only when batch is pushed out. 
        Interlocked.Increment(ref _queuedBatchCount);
        EnvironmentAdapter.Instance.RunTask(() => {
          CreateBatchAndBroadcast(buffer, trigger);
          Interlocked.Decrement(ref _queuedBatchCount);
        });
      }
    }

    private void CreateBatchAndBroadcast(BatchBuffer buffer, BatchTrigger trigger) {
      var start = AppTime.GetTimestamp();
      buffer.FlushedItems = buffer.GetAll();
      var batch = new Batch<T>() {
        Action = UploaderAction.Batch,
        DataId = BatchInfo.NextDataId(),
        Items = buffer.FlushedItems,
        ItemCount = buffer.FlushedItems.Count,
        CreatedOn = AppTime.UtcNow,
        Comment = $"Triggered by: {trigger}",
        Duration = AppTime.GetDuration(start)
      };
      //we are on background thread already
      Broadcast(batch, BroadcastMode.Sync);
      _lastFlushedOn = AppTime.UtcNow;
    }

    /// <summary>Signals that the upload operations started, with components initialized. </summary>
    public void Start() {
    }

    /// <summary>
    ///     Signals that the domain shutdown started. Forces the queue to send out the batch
    ///     with all accumulated items.
    /// </summary>
    public void Shutdown() {
      _inShutdown = true;
      Flush(_buffer, BatchTrigger.Completed);
    }

    /// <summary>
    ///     Returns true if the queue is empty.
    /// </summary>
    /// <returns>True if the queue is empty; otherwise, false.</returns>
    public bool IsEmpty() {
      return _buffer.ItemCount == 0;
    }

    /// <summary>Signals that upload operation is completing. Causes queue flush in sync mode. </summary>
    public void OnCompleted() {
      if (!IsEmpty()) {
        Flush(_buffer, BatchTrigger.Completed);
      }
    }

    /// <summary>
    ///     An empty implementation of the <see cref="IObserver{T}.OnError(Exception)"/> method.
    /// </summary>
    /// <param name="error">The exception object.</param>
    public void OnError(Exception error) {
    }

    /// <summary>
    ///     Flushes the queue. Forces the queue to immediately send out a batch with all items. 
    /// </summary>
    public void Flush() {
      Flush(_buffer, BatchTrigger.Code);
    }

    private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
      if (ShouldIgnoreTimerFlush()) {
        return;
      }
      Flush(_buffer, BatchTrigger.Timer);
    }

    private bool ShouldIgnoreTimerFlush() {
      if (IsEmpty()) {
        return true;
      }

      // if there was a recent flush, skip this timer flush
      if (_flushTimer != null && _lastFlushedOn.AddMilliseconds(_flushTimer.Interval) > AppTime.UtcNow) {
        return false;
      }
      // check how long was oldest item sitting in the queue; if less than max linger time, do not flush
      if (_config.MaxItemLingerTime != null) {
        var lingerTime = _config.MaxItemLingerTime.Value;
        if (_buffer.FirstReceivedAt.Add(lingerTime) > AppTime.UtcNow) {
          return true;
        }
      }
      return false;
    }


    private void BroadcastSingleItem(T item) {
      var batch = new Batch<T>() {
        Action = UploaderAction.Batch,
        DataId = BatchInfo.NextDataId(),
        Items = new[] { item },
        ItemCount = 1,
        CreatedOn = AppTime.UtcNow,
        Comment = "Single item pass-thru",
        Duration = TimeSpan.Zero
      };
      Broadcast(batch, BroadcastMode.Sync);
      _lastFlushedOn = AppTime.UtcNow;
    }

  }


}
