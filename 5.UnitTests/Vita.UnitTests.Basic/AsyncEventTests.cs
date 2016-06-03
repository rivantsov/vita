using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;

namespace Vita.UnitTests.Basic {
  [TestClass]
  public class AsyncEventTests {

    public class TestEventArgs : EventArgs {
      public int Value;
    }

    //Let's test it with interface
    public interface IEventSource {
      event AsyncEvent<TestEventArgs> TestEvent; 
    }

    public class AsyncEventSource : IEventSource {
      public event AsyncEvent<TestEventArgs> TestEvent;

      internal async Task RaiseEvent(TestEventArgs args) {
        await TestEvent.RaiseAsync(this, args); 
      }
    }//class

    // We have global counter; it will be incremented in event handlers
    int _counter;

    [TestMethod]
    public void TestAsyncEvents() {
      var evtSource = new AsyncEventSource();
      evtSource.TestEvent += AsyncEventsTest_AsyncHandler;
      evtSource.TestEvent += AsyncEventsTest_SyncHandler;
      var args = new TestEventArgs() { Value = 2 }; // incr value; our counter should be incremented twice by 2 - in two event handlers
      _counter = 0; 
      AsyncHelper.RunSync(() => evtSource.RaiseEvent(args));
      // now _counter should be 4
      Assert.AreEqual(4, _counter, "Expected _counter set to 4");
    }//test method

    private async Task AsyncEventsTest_AsyncHandler(object source, TestEventArgs args) {
      await Task.Run(() => _counter += args.Value);
    }

    // Sync handler should return null as Task - this is handled correctly
    private Task AsyncEventsTest_SyncHandler(object source, TestEventArgs args) {
      _counter += args.Value;
      return null; 
    }

  }//class
} //ns
