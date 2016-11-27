using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Modules.JobExecution;
using System.Threading;
using System.Diagnostics;
using Vita.Common;
using Vita.Entities.Services;

namespace Vita.UnitTests.Basic {
  [TestClass]
  public class JobRunnerTests {

    public class JobTestApp : EntityApp {
      public IJobExecutionService JobService;

      public JobTestApp() {
        var area = AddArea("jobs");
        JobService = new JobExecutionModule(area);
        var incidentLog = new Vita.Modules.Logging.IncidentLogModule(area);
        var errLog = new Vita.Modules.Logging.ErrorLogModule(area);
      }
    }//class

    JobTestApp _jobApp;
    static string _successMessage = "Completed successfully!";
    ITimerServiceControl _timersControl;

    [TestInitialize]
    public void TestInit() {
      _jobApp = new JobTestApp();
      Startup.ActivateApp(_jobApp);
      Startup.DeleteAll(_jobApp, typeof(IJobRun), typeof(IJob));
      _timersControl = _jobApp.GetService<ITimerServiceControl>();
      _timersControl.EnableAutoFire(false); //disable firing automatically, only thru FireAll
    }

    [TestCleanup]
    public void TestCleanup() {
      _jobApp.Shutdown();
      _jobApp = null; 
    }

    [TestMethod]
    public void TestJobService() {
      try {
        TestPoolJobs();
        /* To fix - fails when try to run multiple times
        TestPoolJobs();
        TestCleanup();
        TestInit();
        TestPoolJobs();
        TestCleanup();
        TestInit();
        TestPoolJobs();
        TestPoolJobs();
        */ 
      } finally {
        _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
      }
    }

    public void TestPoolJobs () {
      _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
      _syncCallCount = 0;
      _asyncCallCount = 0; 
      var session = _jobApp.OpenSystemSession();
      // these lists are passed as parameter and one element is removed on each task start attempt
      // the list is serialized back into db, so next call gets shorter list
      var listParam1 = new List<string>() { "a", "b", "c", "d", "e", "f" };
      var listParam2 = new List<string>() { "1", "2", "3", "4", "5", "6" };
#pragma warning disable 4014
      // c# compiler gives a warning here about calling async function (SampleJobAsync); however, we do not actually call it here, 
      // we just grab the calling expression to 'save it' in the database and invoke it properly later.
      var retryPolicy = new JobRetryPolicy(30, 5, 1, 0);
      var asyncJob = _jobApp.JobService.CreatePoolJob(session, "TestAsyncJob", (ctx) => DoSampleJobAsync(ctx, listParam1, "Hello async world!", 5),
          flags: JobFlags.PersistArguments | JobFlags.StartOnSave, retryPolicy: retryPolicy);
      // normally pool job should async, but sync method returning fake task is OK too
      var syncJob = _jobApp.JobService.CreatePoolJob(session, "TestSyncJob", (ctx) => DoSampleJob(ctx, listParam2, "Hello sync world!", 5),
          flags: JobFlags.PersistArguments | JobFlags.StartOnSave,  retryPolicy: retryPolicy);
#pragma warning restore 4014
      session.SaveChanges();

      // the job starts immediately for the first run when we call SaveChanges
      // it is coded so that it will fail 3 times then succeed; time between restarts is 30 seconds
      // we will push time forward and force firing timer events to 'compress time' and cause job restarts
      for(int i = 0; i < 10; i++) {
        _jobApp.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(i));
        _timersControl.FireAll();
        Thread.Sleep(20); //let tasks run
      }

      //Thread.Sleep(100);
      // The job must be completed by now. 
      // open fresh session, to see changes
      session = _jobApp.OpenSystemSession(); 
      // Async job
      var asyncJobRun = session.EntitySet<IJobRun>().First(jr => jr.Job.Id == asyncJob.Id);
      Assert.AreEqual(JobRunStatus.Completed, asyncJobRun.Status, "Async job: expected completed status");
      Assert.IsTrue(asyncJobRun.Log.Contains(_successMessage), "Expected success message at the end.");
      // Sync job
      var syncJobRun = session.EntitySet<IJobRun>().First(jr => jr.Job.Id == syncJob.Id);
      Assert.AreEqual(JobRunStatus.Completed, syncJobRun.Status, "Sync job: expected completed status");
      Assert.IsTrue(syncJobRun.Log.Contains(_successMessage), "Expected success message at the end.");

      _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);

    } //method


    // Job methods --------------------------------------------------------------------------------------
    static int _syncCallCount = 0; 
    //sync version
    public static Task DoSampleJob(JobRunContext jobContext, List<string> listPrm, string arg0, int arg1) {
      _syncCallCount++;
      //this updates immediately in the database
      jobContext.UpdateProgress( _syncCallCount, "Processing, count: " + _syncCallCount); 
      listPrm.RemoveAt(0);
      if(_syncCallCount < 3)
        throw new Exception("Sync task failed, call count: " + _syncCallCount);
      //success on 3d run
      jobContext.UpdateProgress(100, _successMessage);
      return Task.CompletedTask; 
    }

    static int _asyncCallCount = 0;
    //async version
    public static async Task DoSampleJobAsync(JobRunContext jobContext, List<string> listPrm, string arg0, int arg1) {
      _asyncCallCount++;
      await Task.Delay(10); //just to play with async execution 
      listPrm.RemoveAt(0);
      //this updates immediately in the database
      jobContext.UpdateProgress(_syncCallCount, "Processing, count: " + _syncCallCount);
      if(_asyncCallCount < 3 ) 
        throw new Exception("Async task failed, call count: " + _syncCallCount);
      jobContext.UpdateProgress(100, _successMessage);
      await Task.Delay(10);
    }

    [TestMethod]
    public void TestJobServiceLightAsyncTask() {
      try {
        AsyncHelper.RunSync(() => TestLightTaskAsync());
      } finally {
        _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
      }
    }

    private async Task TestLightTaskAsync() {
      _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
      /* to get all jobs
      var allJobs = _jobApp.JobService.GetRunningJobs();
      Assert.IsTrue(allJobs.Count == 0, "Expected no jobs running");
      */
      var ctx = _jobApp.CreateSystemContext();
      _lightCallCount = 0;
      _lightJobFailed = false; 
      //Run with 2 initial failures; on first failure the task will be persisted; second run will be done after delay, from data saved in DB
      // the third run will succeed. 
      var jobContext = await JobHelper.ExecuteWithRetriesAsync(ctx, (jobCtx) => LightJob("abc", 2));
      Assert.IsTrue(_lightJobFailed, "Expected light job to fail.");

      for(int i = 0; i < 40; i++) {
        _jobApp.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(i));
        _timersControl.FireAll();
        Thread.Sleep(20); //let tasks run
      }

      Assert.IsFalse(_lightJobFailed, "Expected light job to succeed.");
      Assert.AreEqual(3, _lightCallCount, "Expected 3 attempts of light job.");

      //Now run without failures
       _lightCallCount = 0; 
       jobContext = await _jobApp.JobService.RunLightTaskAsync(ctx, (jobCtx) => LightJob("abc", 0), "SampleLightTask"); //do not fail at all
      Thread.Sleep(20); //let tasks run
      Assert.IsFalse(_lightJobFailed, "Expected light job succeed without retries.");
      _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
    }

    static int _lightCallCount;
    static bool _lightJobFailed;
    static object _lock = new object(); 

    private static async Task LightJob(string arg0, int failCount) {
      lock(_lock) {
        _lightCallCount++;
        if(_lightCallCount <= failCount) {
          _lightJobFailed = true;
          Debug.WriteLine("Failing light task...");
          throw new Exception("Light task error!");
        }
        _lightJobFailed = false;
        Debug.WriteLine("light task completed! ");
      }
      await Task.Delay(10);
    }


    [TestMethod]
    public void TestJobServiceBackgroundJob() {
      try {
        TestBackgroundJob();
      } finally {
        _jobApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
      }
    }

    static int _bkJobCallCount;
    static bool _bkJobFailed;

    //Note: this test fails when you use breakpoints
    private void TestBackgroundJob() {
      _bkJobCallCount = 0;
      _bkJobFailed = false;
      var session = _jobApp.OpenSystemSession();
      var job = _jobApp.JobService.CreateBackgroundJob(session, "LongJob", (jobCtx) => BackgroundJob(jobCtx, "abc", 10));
      session.SaveChanges(); //this will start the job because we have flag StartOnSave (it is default)
      Thread.Sleep(10);
      Assert.IsTrue(_bkJobFailed, "Expected background job to fail initially");
      // Push time forward and imitate firing timer events
      for(int i = 0; i < 100; i++) {
        _jobApp.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(i));
        _timersControl.FireAll();
        Thread.Sleep(20); //let tasks run
      }
      Assert.IsFalse(_bkJobFailed, "Expected background job to succeed at the end.");
    }

    private static void BackgroundJob(JobRunContext context, string arg0, int arg1) {
      _bkJobCallCount++;
      if(_bkJobCallCount < 3) {
        _bkJobFailed = true;
        var msg = "Background job failed, callCount: " + _bkJobCallCount;
        context.UpdateProgress(0, msg);
        throw new Exception(msg);
      }
      for(int i = 0; i < 100; i++) {
        context.UpdateProgress(i, "Processing, i= " + i);
        Thread.Sleep(10);
      }
      _bkJobFailed = false; 
    }

  }//class
}//ns
