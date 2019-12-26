using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Vita.Entities.Logging {

  [DebuggerDisplay("Count = {Count}")]
  /// <summary>
  ///   Implements a batching queue - concurrent no-lock enqueue-one; dequeue many with lock.
  ///   This is enhanced version - with pooling Node objects for later reuse. 
  /// </summary>
  public class BatchingQueueEnhanced<T> {
    public int Count => _count;

    class Node {
      public T Item;
      public volatile Node Next;
    }

    // We use Interlocked operations when accessing last-in (to push items); 
    // we use lock when accessing _lastOut element (dequeuing multiple items)
    volatile Node _lastIn; //must be marked as volatile for interlocked ops
    Node _lastOut;
    object _dequeueLock = new object();
    volatile int _count;

    public BatchingQueueEnhanced() {
      // There's always at least one fake node in linked list
      _lastIn = _lastOut = new Node();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Enqueue(T item) {
      var node = NodePoolTryPop() ?? new Node();
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
          var oldLastOut = _lastOut; 
          _lastOut = _lastOut.Next;
          NodePoolTryPush(oldLastOut);
          list.Add(_lastOut.Item);
          _lastOut.Item = default(T); //clear the ref to data 
          Interlocked.Decrement(ref _count);
        }
        return list;
      } //lock
    } //method

    #region Node pooling
    // We pool/reuse Node objects; we save nodes in a simple concurrent stack. 
    // The stack is not 100% reliable - it might fail occasionally when pushing/popping up nodes
    Node _nodePoolHead;

    private Node NodePoolTryPop() {
      var head = _nodePoolHead;
      if(head == null)
        return null; // stack is empty
      if(Interlocked.CompareExchange(ref _nodePoolHead, head.Next, head) == head) {
        head.Next = null; //drop the 
        return head;
      }
      return null; 
    }

    private void NodePoolTryPush(Node node) {
      // Save only 95% of nodes, so the saved nodes pool slowly drains
      if(_count % 20 == 0)
        return; 
      node.Next = _nodePoolHead;
      // we make just one attempt; if it fails, we don't care - node will be GC-d
      Interlocked.CompareExchange(ref _nodePoolHead, node, node.Next);
    }
    #endregion

  } //class
}
