using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Vita.Entities.Logging {

  /// <summary>
  /// Implements a batching queue - concurrent, no-lock enqueue-one, dequeue many 
  /// </summary>
  public class BatchingQueue<T> {
    public int Count => _count;

    class LinkedNode {
      public T Item { get; set; }
      public LinkedNode Next { get; set; }
    }

    // We use Interlocked operations when accessing last-in; we use lock when accessing first-in element
    LinkedNode _lastIn;
    LinkedNode _lastOut;
    object _dequeueLock = new object();
    int _count;

    public BatchingQueue() {
      // There's always at least one 'empty' node in linked list
      _lastIn = _lastOut = new LinkedNode();
    }
    public int Enqueue(T item) {
      var node = new LinkedNode() { Item = item };
      var prevLastIn = Interlocked.Exchange(ref _lastIn, node);
      prevLastIn.Next = node;
      return Interlocked.Increment(ref _count);
    }

    public IList<T> DequeueMany(int maxCount = int.MaxValue) {
      // iterate over list starting with _firstIn
      var list = new List<T>();
      lock(_dequeueLock) {
        while(_count > 0 && list.Count < maxCount) {
          _lastOut = _lastOut.Next;
          list.Add(_lastOut.Item);
          _lastOut.Item = default(T); //clear the ref to data 
          Interlocked.Decrement(ref _count);
        }
        return list;
      } //lock
    } //method

  } //class
}
