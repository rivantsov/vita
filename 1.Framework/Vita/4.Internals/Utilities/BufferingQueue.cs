using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Vita.Entities.Utilities.Internals;

namespace Vita.Entities.Utilities {

  /// <summary>A wrapper around ConcurrentQueue class allowing Get-all-and-clear operation synchronized 
  /// with concurrent Enqueue operations without data loss.</summary>
  /// <typeparam name="T">Queue item type.</typeparam>
  public class BufferingQueue<T> {
    /*
     The challenge with ConcurrentQueue to implement buffering is that there's no atomic get-all-and-clear operation. You have to use queue.ToArray()
       and then queue.Clear() (or create new Queue). In between these 2 actions there might be Enqueue calls, and their data might 
       be lost. We need an extra sync/blocking facility to block out Enqueu calls while we grab-all.   
       This class use 2 switches to achieve this:
          _blocked - if set, it blocks writers (Enqueue callers) from entering the operation; they must wait spinning until the flag is dropped.
          _writersCount - number of Enqueu callers currently in action. The Enqueue call increments this field on entry and decrements on exit. 
        The GetAllAndClear method sets _blocked flag, and then checks/waits until _writersCount drops to zero. At this moment it has full control over 
        underlying queue - if there are any active attempts to Enqueue item(s), these threads are spinning and waiting for the _blocked flag to drop. 
        Once GetAllAndClear completes copying all values, it clears the queue and resets the _blocked flag and returns all copied values.
       The extra cost for Enqueue calls is 1 interlocked.Exchange and 2 bool checks
     */
    ConcurrentQueue<T> _queue;
    int _length; //fast count, faster than _queue.Count
    // _writersCount has to be long as we access it using Interlocked.Read and there's only for long arg - see GetAllAndClear method
    long _writersCount;
    bool _blocked;
    object _readLock = new object();

    public BufferingQueue() {
      _queue = new ConcurrentQueue<T>();
    }

    public void Enqueue(T item) {
      if (TryEnqueue(item))
        return;
      var spinWait = new SpinWait();
      while (true) {
        spinWait.SpinOnce();
        if (TryEnqueue(item))
          return;
      }
    } //method

    public IList<T> GetAllAndClear() {
      // we use regular lock for GetAll as it is called much less often than Enqueue, so perf is not critical here
      lock (_readLock) {
        // block adding and then wait for writers count become zero
        _blocked = true;
        var spinWait = new SpinWait();
        // !!! Extremely important - we must use Interlocked read here, otherwise the whole schema breaks down and we start loosing items !!!
        // there's only one Read overload - for long parameter, that's why we make _writersCount long
        while (Interlocked.Read(ref _writersCount) > 0)
          spinWait.SpinOnce();

        TestHelperConditional.RandomYield(5); // for testing

        // we are now in safe place - all writers are now spining and waiting, we have exclusive access to the queue
        var result = _queue.ToArray();
        // _queue.Clear(); // .NET Standard - coming in the future
        _queue = new ConcurrentQueue<T>(); //.NET full Fx
        _length = 0;
        _blocked = false;
        return result;
      } // lock
    } //method

    /// <summary>Returns the number of items in the buffer. </summary>
    public int Length => _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] //hopefully compiler will inline the call
    private bool TryEnqueue(T item) {
      TestHelperConditional.RandomYield(5);
      if (_blocked)
        return false;
      TestHelperConditional.RandomYield(5);
      Interlocked.Increment(ref _writersCount);
      try {
        if (_blocked)
          return false; //do it again, in case we passed the first check, froze, ad GetAll set _blocked = true and saw _writersCount == 0; 
        _queue.Enqueue(item);
        Interlocked.Increment(ref _length);
        return true;
      } finally {
        Interlocked.Decrement(ref _writersCount);
      }
    } //method

  } //class
}