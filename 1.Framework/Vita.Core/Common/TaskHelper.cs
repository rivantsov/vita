using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Common {

  /// <summary>Provides helper method to run asynchronous methods  synchronously.</summary>
  /// <remarks>There is a big problem out there with introduction of async methods. If you have async
  /// method, you are practically forced to call it with await from another async method. However, 
  /// with a lot of existing code not changed overnight, you often need to call the new-style async 
  /// method from old-style sync method. And it is not so simple, it turns out. Task.Run seems to work 
  /// in unit tests (console apps) but results in thread deadlock under ASP.NET. There are several hacks 
  /// out there, different level of complexity, all non-trivial. Here's my version, trivial and 
  /// seems to be working. It might be not very efficient resource-wise, but this is not the biggest priority.
  /// At some point the goal was to just find something working reliably, and this solution seems to be it.</remarks>
  public static class TaskHelper {

    class ResultBox<T> {
      public T Result;
      public bool Completed;
      public Exception Exception;
    }

    public static T RunSync<T>(Func<Task<T>> func) {
      var rbox = new ResultBox<T>();
      ThreadPool.QueueUserWorkItem(async (d) => {
        try {
          rbox.Result = await func();
          rbox.Completed = true;
        } catch (Exception ex) {
          rbox.Exception = ex;
          rbox.Completed = true;
        }
      });
      while (!rbox.Completed)
        Thread.Yield();
      CheckException(rbox.Exception);
      return rbox.Result;
    }

    public static void RunSync(Func<Task> action) {
      var rbox = new ResultBox<object>();
      ThreadPool.QueueUserWorkItem(async (d) => {
        try {
          await action();
          rbox.Completed = true;
        } catch (Exception ex) {
          rbox.Exception = ex;
          rbox.Completed = true;
        }
      });
      while (!rbox.Completed)
        Thread.Yield();
      CheckException(rbox.Exception); 
    }

    private static void CheckException(Exception ex) {
      if (ex == null) return;
      // if it is 'soft' exc, like ClientFaultExc, throw it as-is; we don't care about stack, but handlers may expect certain exc type
      // otherwise wrap it into aggregate exception (to preserve original call stack) - standard practice
      if (IsNoWrap(ex.GetType()))
        throw ex;
      throw new AggregateException(ex.Message, ex);
    }


    static HashSet<Type> _noWrapExceptions = new HashSet<Type>(new [] {typeof(OperationAbortException)});
    static object _lock = new object();

    /// <summary>Register exception(s) as 'NoWrap', to rethrow them as-is when passing asyn-> sync boundary.</summary>
    /// <param name="exceptionTypes">Exception types.</param>
    /// <remarks>Simplifies handling some specific exceptions for calling code.
    /// By default, exception on pool thread (async side) is rethrown on calling thread
    /// as wrapped into AggregateException, to preserve original exception's call stack. 
    /// But for some exceptions, like ClientFaultException (client error), we do not need call stack and 
    /// it is easier to set cach block for this exception type if exception is rethrown as is. 
    /// By default, the no-wrap set contains OperationAbortException, which causes this exception and 
    /// derived ClientFaultException to rethrown as-is on the calling thread. 
    /// </remarks>
    public static void AddNoWrapExceptions(params Type[] exceptionTypes) {
      lock (_lock) {
        _noWrapExceptions.UnionWith(exceptionTypes); 
      }
    }
    public static bool IsNoWrap(Type excType) {
      lock (_lock) {
        return _noWrapExceptions.Any(t => t.IsAssignableFrom(excType));
      }
    }
 
  }//class
}
