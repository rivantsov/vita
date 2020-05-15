using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Utilities {

  /*
  Avoiding deadlocks when calling async from sync - aka SyncAsync bridge. 
  Problem: calling async APIs from sync code - may result in deadlocks in some environments. 
  Here is an often-used solution:
               var data = httpClient.PostAsync(message).Result;
       Or enhanced version:
               var data = httpClient.PostAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
  While it works OK in console or service app, it deadlocks in ASP.NET and ASP.NET Core application. 
  More details here: 
             https://medium.com/rubrikkgroup/understanding-async-avoiding-deadlocks-e41f8f2c6f5d    
  According to the article, the best way to go is to use an extra foreground thread, and schedule wait there.          
  This is what the code here is implementing. 
  I did test it under ASP.NET and ASP.NET core, and it works without deadlocks. 
   */

  /// <summary>
  ///     Sync-async bridge. Provides methods for calling async methods from sync code, reliably without deadlocks, in any hosting environment.  
  /// </summary>
  public static class SyncAsync {

    class AsyncJob<T> {
      public Func<Task<T>> JobFunc;
      public T Result;
      public ManualResetEventSlim CompletedSignal;
      public Exception Exception;
    }

    public static TResult RunSync<TResult>(Func<Task<TResult>> func) {
      var job = new AsyncJob<TResult>() { JobFunc = func, CompletedSignal = new ManualResetEventSlim(false) };
      var thread = new Thread(ExecuteJob<TResult>);
      thread.Start(job);
      job.CompletedSignal.Wait();
      if (job.Exception != null)
        throw job.Exception;
      return job.Result;
    }

    public static void RunSync(Func<Task> func) {
      Func<Task<int>> funcX = async () => { await func(); return 0; };
      RunSync(funcX);
    }

    private static void ExecuteJob<TResult>(object job) {
      var jobT = (AsyncJob<TResult>)job;
      try {
        jobT.Result = jobT.JobFunc().Result;
        // Note to myself - do not try to catch AggrExc and unwrap it here - this destroys call stack, and other bad stuff
        //  taking and rethrowing as-is is the best option
      } catch (Exception ex) {
        jobT.Exception = ex;
      }
      jobT.CompletedSignal.Set();
    }
  }
}
