using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vita.Entities.Utilities;
using System.Threading.Tasks;
using System.Threading;
using Vita.Entities.Utilities.Internals;
using System.Collections.Generic;

namespace Vita.Testing.BasicTests {

  [TestClass]
  public class BufferingQueueTests {
    public class TestItem {
      public string Name;
      public TestItem(string name) { Name = name; }
    }

    BufferingQueue<TestItem> _queue;
    int _currentCount;
    int _numBatches;
    int _maxBatchSize;
    bool _running;

    // testing experimental class BufferingQueue - like concurrent queue, but with Get-all-and-clear method
    // so far perf results do not warrant its use for log buffering
    [TestMethod]
    public void TestBufferingQueue() {
      int threadCount = 10;
      int itemCount = 50 * 1000;
      TestHelperConditional.Enable(true);

      _queue = new BufferingQueue<TestItem>();
      _running = true;
      var readTask = Task.Run(() => LoopReadAllFromQueue());

      var enqueueTasks = new List<Task>();
      for (int i = 0; i < threadCount; i++)
        enqueueTasks.Add(Task.Run(() => EnqueueItems(itemCount)));
      Task.WaitAll(enqueueTasks.ToArray());

      Thread.Sleep(100);
      _running = false;
      Thread.Sleep(100);

      Debug.WriteLine($"# of read batches: {_numBatches}");
      Debug.WriteLine($"Max batch size: {_maxBatchSize}");
      Assert.AreEqual(0, _currentCount);
    }

    private void EnqueueItems(int count) {
      for (int i = 0; i < count; i++) {
        _queue.Enqueue(new TestItem("N" + i));
        Interlocked.Increment(ref _currentCount);
        if (i % 5 == 0)
          Thread.Yield();
      }
    }

    private void LoopReadAllFromQueue() {
      while (_running) {
        ReadAllFromQueue();
        Thread.Yield();
        //Thread.Sleep(1); 
      }
    }

    private void ReadAllFromQueue() {
      var items = _queue.GetAllAndClear();
      Interlocked.Add(ref _currentCount, -items.Count);
      if (items.Count > 0) {
        _numBatches++;
        _maxBatchSize = Math.Max(_maxBatchSize, items.Count);
      }

    }
  }

}
