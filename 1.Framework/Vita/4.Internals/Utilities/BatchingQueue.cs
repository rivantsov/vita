using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Utilities {

  public class QueueBatchEventArgs<T>: EventArgs {
    public readonly IList<T> Items; 
    public QueueBatchEventArgs(IList<T> items) {
      Items = items;
    }
  }

  public class BatchingQueue<T> {

    public event EventHandler<QueueBatchEventArgs<T>> Batched;

    int _batchSize;
    int _maxLingerMs;
    System.Timers.Timer _timer;
    bool _batchedSinceLastTimer; // true if there was Flush() call since last timer tick. 
    int _count; //dupe of _queue.Count, only faster

    ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
    object _dequeueLock = new object();
    private IList<T> _emptyList = new List<T>();

    public BatchingQueue(int batchSize = 1000, int maxLingerMs = 200) {
      _batchSize = batchSize;
      _maxLingerMs = maxLingerMs;
      if (_maxLingerMs > 0) {
        _timer = new System.Timers.Timer(maxLingerMs / 2);
        _timer.Elapsed += Timer_Elapsed;
      }
    }

    public int Count => _count;

    public void Enqueue(T item) {
      if (item == null)
        return;
      _queue.Enqueue(item);
      var count = Interlocked.Increment(ref _count);
      if (count >= _batchSize)
        StartProduceBatch();
    }

    private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
      var batchedRecently = _batchedSinceLastTimer;
      _batchedSinceLastTimer = false;
      if (batchedRecently)
        return;
      StartProduceBatch(); 
    }

    private void StartProduceBatch() {
      Task.Run(() => ProduceBatch());
    }

    public IList<T> ProduceBatch() {
      lock (_dequeueLock) {
        _batchedSinceLastTimer = true;
        var items = new List<T>();
        while (_count > 0 && _queue.TryDequeue(out T item)) {
          items.Add(item);
          Interlocked.Decrement(ref _count);
        }
        Batched?.Invoke(this, new QueueBatchEventArgs<T>(items));
        return items; 
      }
    }

    // imprecise, might read all in the middle of enqueueing
    public IList<T> GetAll() {
      lock(_dequeueLock) {
        return _queue.ToArray();
      }
    }

  }
}
