using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Vita.Internals.Utilities {

  public interface ILinkedNode {
    ILinkedNode Next { get; set; }
  }

  // Implements batching queue - concurrent no-lock push, batched pull/dequeue with lock

  public class BufferingNodeQueue {
    public int Count => _count;

    ILinkedNode _last;
    ILinkedNode _first;
    int _count;
    object _lock = new object();

    public void EnqueueNode(ILinkedNode node) {
      var prevLast = Interlocked.Exchange(ref _last, node);
      if (prevLast == null)
        SetFirst(node);
      else
        prevLast.Next = node;
      Interlocked.Increment(ref _count);
    }

    public IList<ILinkedNode> DequeueNodes(int maxCount = int.MaxValue) {
      lock (_lock) {
        var list = new List<ILinkedNode>();
        while (list.Count < maxCount && _first != null) {
          list.Add(_first);
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

  public class BufferingQueue<T>: BufferingNodeQueue {

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
