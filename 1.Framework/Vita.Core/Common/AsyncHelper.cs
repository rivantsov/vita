using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Vita.Common {

  /// <summary>Defines an async event delegate. </summary>
  /// <typeparam name="T">Event args type.</typeparam>
  /// <param name="source">Event source object.</param>
  /// <param name="args">Event args object.</param>
  /// <returns>Task.</returns>
  public delegate Task AsyncEvent<T>(object source, T args) where T : EventArgs;

  /// <summary>Provides helper method to run asynchronous methods  synchronously; also defines AsyncEvent helper methods.</summary>
  /// <remarks>There is a big problem out there with introduction of async methods. If you have async
  /// method, you are practically forced to call it with await from another async method. However, 
  /// with a lot of existing code not changed overnight, you often need to call the new-style async 
  /// method from old-style sync method. And it is not so simple, it turns out. Task.Run seems to work 
  /// in unit tests (console apps) but results in thread deadlock under ASP.NET. There are several hacks 
  /// out there, different level of complexity, all non-trivial. Here's my version, trivial and 
  /// seems to be working. It might be not very efficient resource-wise, but this is not the biggest priority.
  /// At some point the goal was to just find something working reliably, and this solution seems to be it.</remarks>
  public static class AsyncHelper {

    /// <summary>Raises an async event. </summary>
    /// <typeparam name="T">Event args type.</typeparam>
    /// <param name="asyncEvent">The event instance. </param>
    /// <param name="source">The event source.</param>
    /// <param name="args">EventArgs object.</param>
    /// <param name="wait">Optional. If True, method awaits all handlers.</param>
    /// <returns>Awaitable task.</returns>
    public async static Task RaiseAsync<T>(this AsyncEvent<T> asyncEvent, object source, T args, bool wait = true) where T : EventArgs {
      var handlers = asyncEvent.GetInvocationList();
      var tasks = new List<Task>();
      foreach(var handler in handlers) {
        var typedHandler = (AsyncEvent<T>)handler;
        var task = typedHandler(source, args);
        if(task != null) //handler can return null
          tasks.Add(task);
      }
      if(wait && tasks.Count > 0)
        await Task.WhenAll(tasks);
    }

    // Sync/Async bridge ==========================================================================================================================

    public static T RunSync<T>(Func<Task<T>> func, bool unwrapException = true) {
      try {
        var result = Task.Factory.StartNew(func).Unwrap().GetAwaiter().GetResult();
        return result;
      } catch(AggregateException exc) {
        if(unwrapException)
          throw exc.InnerExceptions[0];
        else 
          throw; 
      }
    }

    public static void RunSync(Func<Task> action, bool unwrapException = true) {
      try {
        Task.Factory.StartNew(action).Unwrap().GetAwaiter().GetResult();
      } catch(AggregateException exc) {
        if(unwrapException)
          throw exc.InnerExceptions[0];
        else
          throw;
      }
    }

  }//class
}
