using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Vita.Entities.Logging {

  [DebuggerDisplay("Count = {Count}")]
  /// <summary>
  ///   Implements a batching queue - concurrent no-lock enqueue-one; dequeue many with lock
  /// </summary>
  public sealed class BatchingQueue<T> {
    public int Count => _count;

    class Node {
      public T Item;
      public volatile Node Next;
    }

    // We use Interlocked operations when accessing last-in; we use lock when accessing first-in element
    volatile Node _lastIn; //must be marked as volatile for interlocked ops
    Node _lastOut;
    object _dequeueLock = new object();
    volatile int _count;

    public BatchingQueue() {
      // There's always at least one empty node in linked list; so empty queue holds just one node.
      //  this is necessary to avoid problem with pushing first node (or popping the last one) - when you have 
      // to modify both pointers to start and end of the list from null to this first element.  
      _lastIn = _lastOut = new Node();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Enqueue(T item) {
      // try get previously saved nodes
      var node = new Node();
      node.Item = item;
      if(Interlocked.CompareExchange(ref _lastIn.Next, node, null) == null) {
        _lastIn = node;
        return Interlocked.Increment(ref _count);
      }
      return EnqueueSlowPath(node);
    }

    private int EnqueueSlowPath(Node node) {
      SpinWait spin = new SpinWait();
      // Keep trying with spin until we succeed.
      do {
        spin.SpinOnce();
      } while(Interlocked.CompareExchange(ref _lastIn.Next, node, null) != null);
      // success, replace last-in ref
      _lastIn = node;
      return Interlocked.Increment(ref _count);
    }

    public IList<T> DequeueMany(int maxCount = int.MaxValue) {
      // iterate over list starting with _lastOut
      var list = new List<T>();
      lock(_dequeueLock) {
        while(_lastOut.Next != null && list.Count < maxCount) {
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
