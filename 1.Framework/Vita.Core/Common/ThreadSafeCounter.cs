using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Common {
  /// <summary>Thread-safe counter; one use is in the JobExecution module to indicate that there are pending actions 
  /// (starting jobs). </summary>
  public class ThreadSafeCounter {
    int _count;

    public int Count {
      get {
        return Interlocked.Add(ref _count, 0);
      }
    }

    public void Reset() {
      Interlocked.Exchange(ref _count, 0);
    }
    public void Add(int value) {
      Interlocked.Add(ref _count, value); 
    }
    public void Increment() {
      Interlocked.Increment(ref _count);
    }
    public void Decrement() {
      Interlocked.Decrement(ref _count);
    }
    public bool IsZero() {
      return  Count == 0;
    }

  }
}
