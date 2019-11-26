using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Vita.Internals.Utilities {

  public interface ILinkedNode {
    ILinkedNode Next { get; set; } 
  }

  // Nov 23, the last, 'the best' version so far
  // Implements batching queue - concurrent no-lock push, batched pull/dequeue with lock
  public class BufferingQueue<TData> {

    class LinkedNode : ILinkedNode {
      public TData Data;
      public LinkedNode Next;
      ILinkedNode ILinkedNode.Next {get;set;}
    }

    ILinkedNode _last;
    ILinkedNode _first;
    int _count;
    object _lock = new object(); 

    public void Enqueue(TData data) {
      var newNode = new LinkedNode() { Data = data };
      var prevLast = Interlocked.Exchange(ref _last, newNode);
      if (prevLast == null)
        SetFirst(newNode);
      else 
        prevLast.Next = newNode;
      Interlocked.Increment(ref _count); 
    }

    public IList<TData> Dequeue(int maxCount = int.MaxValue) {
      lock(_lock) {
        var list = new List<TData>();
        while (list.Count < maxCount && _first != null) {
          list.Add(_first.Data);
          Interlocked.Decrement(ref _count);
          _first = _first.Next; 
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

  }
}
