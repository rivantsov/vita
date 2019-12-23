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
    LinkedNode _firstIn;
    object _firstInLock = new object();
    int _count;

    public int Enqueue(T item) {
      var node = new LinkedNode() { Item = item };
      var prevLastIn = Interlocked.Exchange(ref _lastIn, node);
      if(prevLastIn == null) {
        // queue was empty, assign _firstIn element as well; protect access with lock
        lock(_firstInLock)
          _firstIn = node;
      } else
        prevLastIn.Next = node;
      return Interlocked.Increment(ref _count);
    }

    public IList<T> DequeueMany(int maxCount = int.MaxValue) {
      // iterate over list starting with _firstIn
      var list = new List<T>();  
      lock(_firstInLock) {
        while(_firstIn != null && list.Count < maxCount) {
          list.Add(_firstIn.Item); 
          var next = _firstIn.Next; // save in local var
          _firstIn.Next = null; // break links between nodes
          _firstIn = next;
          Interlocked.Decrement(ref _count);
        }
      } //lock
      return list; 
    } //method

  } //class
}
