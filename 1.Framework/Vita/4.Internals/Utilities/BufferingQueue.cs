using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Vita.Internals.Utilities {

  /// <summary>
  /// Implements a queue of linked nodes - concurrent no-lock push, batched pull/dequeue with lock 
  /// </summary>
  public class LinkedQueue {

    public interface ILinkedNode {
      ILinkedNode Next { get; set; }
    }

    public int Count => _count;

    ILinkedNode _last;
    ILinkedNode _first;
    int _count;
    object _lock = new object();

    public void EnqueueNode(ILinkedNode node) {
      var prevLast = Interlocked.Exchange(ref _last, node);
      if(prevLast == null)
        SetFirst(node);
      else
        prevLast.Next = node;
      Interlocked.Increment(ref _count);
    }

    public IList<ILinkedNode> DequeueNodes(int maxCount = int.MaxValue) {
      return DequeueNodes<ILinkedNode>(maxCount); 
    }
    public IList<TNode> DequeueNodes<TNode>(int maxCount = int.MaxValue) where TNode: ILinkedNode {
      lock (_lock) {
        var list = new List<TNode>();
        while (list.Count < maxCount && _first != null) {
          list.Add((TNode) _first);
          var next = _first.Next;
          _first.Next = null; // break links between nodes
          _first = next;
          Interlocked.Decrement(ref _count);
        }
        return list;
      } //lock
    } //method

    // Used by Enqueue when pushing the first element
    private void SetFirst(ILinkedNode first) {
      lock (_lock) {
        _first = first;
      }
    }
  } //class

  public class BufferingQueue<T>: LinkedQueue {

    class LinkedNode : ILinkedNode {
      public T Item { get; set; }
      public ILinkedNode Next;
      ILinkedNode ILinkedNode.Next {
        get { return Next; }
        set { Next = value; }
      }
    }

    public void Enqueue(T data) {
      EnqueueNode(new LinkedNode() { Item = data });
    }

    public IList<T> DequeueItems(int maxCount = int.MaxValue) {
      var nodes = DequeueNodes(maxCount);
      var list = nodes.Select(nd => ((LinkedNode)nd).Item).ToList();
      return list; 
    } //method

  }
}
