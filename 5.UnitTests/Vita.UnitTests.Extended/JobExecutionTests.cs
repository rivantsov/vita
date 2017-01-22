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
using Vita.UnitTests.Common;
using Vita.Modules.Login;
using Vita.Samples.BookStore;

namespace Vita.UnitTests.Extended {

  [TestClass]
  public class JobExecutionTests {

    static string SuccessMessage = "Completed successfully!";
    EntityApp _app;
    ITimerServiceControl _timersControl;
    IJobInformationService _jobInfoService; 

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
      _app = Startup.BooksApp;
      _timersControl = _app.GetService<ITimerServiceControl>();
      _jobInfoService = _app.GetService<IJobInformationService>(); 
    }

    [TestMethod]
    public void TestJobExecution_ImmediateJobs() {
      try {
        TestUtil.EnableTimers(_app, false);
        SetTime0(); //we start at fixed time 1am current day, for convenience in debugging
        _timersControl.EnableAutoFire(false); //disable firing automatically, only thru FireAll
        AsyncHelper.RunSync(() => TestImmediateJobs());
      } finally {
        ResetTimeOffset();
        TestUtil.EnableTimers(_app, true);
      }
    }

    private async Task TestImmediateJobs() {
      SetTime0(); 
      // Use session associated with particular user. Jobs are retried with the same user context as original attempt. 
      var context = GetUserBoundContext();
      var session = context.OpenSecureSession();

      // We use this arg to test deserialization of arguments
      var listArg = new List<string>() { "a", "b", "c", "d", "e", "f" };
      // Set explicitly the default RetryPolicy
      var jobExecConfig = _app.GetConfig<JobModuleSettings>();
      jobExecConfig.DefaultRetryPolicy = new RetryPolicy(new[] { 2, 2, 2, 2, 2, 2 }); //repeat 6 times with 2 minute intervals

      JobRunContext jobRunContext;
      IJobRun jobRun;
      // 1.a Async job executing successfully 1st time     -------------------------------------------
      jobRunContext = await JobHelper.ExecuteWithRetriesAsync(session.Context, "1a: Sample async job, no failures",
                                  (ctx) => JobMethodAsync(ctx, 0, "Some string argument", listArg));
      Assert.AreEqual(JobRunStatus.Completed, jobRunContext.Status, "Expected Completed for async job");
      // Check that job was not persisted - because it ended successfully first time
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNull(jobRun, "Expected async job succeed and never be persisted.");

      // 1.b Async job initially failing     -------------------------------------------
      jobRunContext = await JobHelper.ExecuteWithRetriesAsync(session.Context, "1b: Sample async job, 3 failures",
                                  (ctx) => JobMethodAsync(ctx, 3, "Some string argument", listArg));
      Assert.AreEqual(JobRunStatus.Error, jobRunContext.Status, "Expected Error");
      // Check that job is persisted - because it ended with error
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNotNull(jobRun, "Expected JobRun record in db.");
      //fast-forward time by 10 minutes and fire timers on the way; 
      FastForwardTimeFireTimers(12);
      // RetryPolicy states to repeat with 2 minutes intervals, 
      // so after 3 failures final run should succeed
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed");
      //attemptNumber is 1-based
      Assert.AreEqual(4, jobRun.AttemptNumber, "Wrong attempt number for successful run.");
      SetTime0();

      // 2.a NoWait job executing successfully 1st time     -------------------------------------------
      jobRunContext = JobHelper.ExecuteWithRetriesNoWait(session.Context, "2a: Sample no-wait job, no failures",
                                  (ctx) => JobMethod(ctx, 0, "Some string argument", listArg));
      Thread.Sleep(100); //make sure job finished
      Assert.AreEqual(JobRunStatus.Completed, jobRunContext.Status, "Expected completed status");
      // Check that job was not persisted - because it ended successfully first time
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNull(jobRun, "Expected no-wait job succeed and never be persisted.");

      // 2.b NoWait job failing initially     -------------------------------------------
      jobRunContext = JobHelper.ExecuteWithRetriesNoWait(session.Context, "2b: Sample no-wait job, 3 failures",
                                  (ctx) => JobMethod(ctx, 3, "Some string argument", listArg));
      Thread.Sleep(100); //make sure job finished
      Assert.AreEqual(JobRunStatus.Error, jobRunContext.Status, "Expected Error status");
      // Check that job is persisted - because it failed
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNotNull(jobRun, "Expected no-wait jobRun record in db.");
      //fast-forward time by 10 minutes; 
      FastForwardTimeFireTimers(12);
      // RetryPolicy states to repeat with 2 minutes intervals, 
      // so after 3 failures final run should succeed
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed");
      //attemptNumber is 1-based
      Assert.AreEqual(4, jobRun.AttemptNumber, "Wrong attempt number for successful run.");
      SetTime0();

      // 3.a Job executed on SaveChanges, executed successfully the first time ------------------------------------
      // This is useful in some cases - job executes ONLY if SaveChanges succeeds; 
      // ex: we send confirmation email in the job, it will be sent only the whole thing succeeds.
      var dataId = new Guid("79C01818-0E1E-4CA1-8D8A-B6E8E6A6897F");
      var data = "Some Job Data";
      var threadType = JobThreadType.Background; // try changing it to pool, just for experiment 
      jobRun = JobHelper.ExecuteWithRetriesOnSaveChanges(session, "3a: Sample on-save job, no failures",
                                     (ctx) => JobMethod(ctx, 0, "Sample string arg", listArg), dataId, data, threadType);
      session.SaveChanges();
      Thread.Sleep(100); //make sure job finished
      // this should be the same as jobRun returned by StartJobOnSaveChanges
      var jobRun2 = jobRun.Job.GetLastFinishedJobRun();
      Assert.IsTrue(jobRun2 == jobRun, "Expected the same job run");
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected Completed status.");

      // 3.b Job executed on SaveChanges, executed with initial failures ------------------------------------
      jobRun = JobHelper.ExecuteWithRetriesOnSaveChanges(session, "3b: Sample on-save job, 3 failures",
                        (ctx) => JobMethod(ctx, 3, "Sample string arg", listArg), dataId, data, threadType);
      session.SaveChanges();
      Thread.Sleep(100); //make sure job finished (with error)
      // get this failed run
      jobRun = jobRun.Job.GetLastFinishedJobRun();
      Assert.AreEqual(JobRunStatus.Error, jobRun.Status, "Expected Error status.");
      //fast-forward time by 10 minutes; 
      FastForwardTimeFireTimers(12);
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed");
      //attemptNumber is 1-based
      Assert.AreEqual(4, jobRun.AttemptNumber, "Wrong attempt number for successful run.");
      SetTime0();


    } //method

    // Job methods --------------------------------------------------------------------------------------

    // failCount parameter specifies number of failures before succeeding
    public static async Task JobMethodAsync(JobRunContext jobContext, int failCount, string stringArg, List<string> listArg) {
      _callCount++; 
      // Check string arg and listPrm are deserialized correctly
      Util.Check(!string.IsNullOrEmpty(stringArg), "StringArg is not deserialized.");
      Util.Check(listArg != null && listArg.Count > 1, "Failed to deserialize list argument.");
      // Throw if we have not reached failCount; note that AttemptNumber is 1-based
      if(jobContext.AttemptNumber <= failCount)
        Util.Throw("Job run failed; target fail count: {0}, attempt number: {0}.", failCount, jobContext.AttemptNumber);
      // for single successful run of ExecuteWithRetriesAsync the UpdateProgress call is ignored
      // - jobRun record is not created 
      jobContext.UpdateProgress(100, SuccessMessage);
      await Task.CompletedTask; //just to have await
    }

    public static void JobMethod(JobRunContext jobContext, int failCount, string stringArg, List<string> listArg) {
      _callCount++;
      // Check string arg and listPrm are deserialized correctly
      Util.Check(!string.IsNullOrEmpty(stringArg), "StringArg is not deserialized.");
      Util.Check(listArg != null && listArg.Count > 1, "Failed to deserialize list argument.");
      // Throw if we have not reached failCount; note that AttemptNumber is 1-based
      if(jobContext.AttemptNumber <= failCount)
        Util.Throw("Job run failed; target fail count: {0}, attempt number: {0}.", failCount, jobContext.AttemptNumber);
      // for single successful run of ExecuteWithRetriesNoWait the UpdateProgress call is ignored
      // - jobRun record is not created 
      jobContext.UpdateProgress(100, SuccessMessage);
    }

    [TestMethod]
    public void TestJobExecution_ScheduledJobs() {
      try {
        _timersControl.EnableAutoFire(false);
       // for(int i = 0; i < 10; i++)
          TestScheduledJobs(0);
      } finally {
        _timersControl.EnableAutoFire(true);
        ResetTimeOffset();
      }
    }

    private void TestScheduledJobs(int runNumber = 0) {
      // Set explicitly the default RetryPolicy
      var jobExecConfig = _app.GetConfig<JobModuleSettings>();
      jobExecConfig.DefaultRetryPolicy = new RetryPolicy(new[] { 2, 2, 2, 2, 2, 2 }); //repeat 6 times with 2 minute intervals
      SetTime0(); //Set fixed current time

      var jobThreadType = JobThreadType.Background; //just to play with it, try it both ways; Background is default

      // We use user-bound context and session (as if user is logged in), to check that retries are executed as 
      // the same user
      var context = GetUserBoundContext();
      var session = context.OpenSecureSession();

      var utcNow = _app.TimeService.UtcNow;
      var halfHourFwd = utcNow.AddMinutes(30);
      var oneHourFwd = utcNow.AddHours(1);
      IJob job;
      IJobRun jobRun;

      // 1. Create and run job at certain time
      jobRun = JobHelper.ExecuteWithRetriesOn(session, "4. Scheduled Job 4", 
             (jobCtx) => ScheduledJob(jobCtx, 0, "123", 5), halfHourFwd, threadType: jobThreadType);
      session.SaveChanges();
      _callCount = 0; 
      // After 25 minutes job should NOT be executed
      SetTimeOffsetFireTimers(25);
      //Thread.Sleep(50);
      AssertCallCount(0, "Expected job not executed.");
      // After 31 minutes job should be executed
      SetTimeOffsetFireTimers(31);
      AssertCallCount(1, "Expected callCount 1 after 31 minutes");

      // 2. Another approach - create job entity, and then schedule it later, to run at certain time
      var ScheduledJobName2 = "5. Scheduled Job #" + runNumber;
      job = JobHelper.CreateJob(session, ScheduledJobName2, (jobCtx) => ScheduledJob(jobCtx, 0, "abc", 10), threadType: jobThreadType);

      // 2.a. Now set the job to run at certain time. For each invocation we can provide some custom data; 
      // pass custom Guid and string; this data is available inside job method in jobRunContext.Data, DataId 
      _callCount = 0;
      var guidData = Guid.NewGuid();
      var stringData = "SomeString";
      jobRun = JobHelper.ScheduleJobRunOn(job, runOnUtc: oneHourFwd, dataId: guidData, data: stringData);
      session.SaveChanges();

      SetTimeOffsetFireTimers(30);       // Move forwad 30 minutes - job should not fire yet
      AssertCallCount(0, "Expected scheduled job not activated.");

      SetTimeOffsetFireTimers(61);       // Move forwad
      AssertCallCount(1, "Expected scheduled call count to increment.");
      Assert.AreEqual(guidData, _receivedDataId.Value, "DataId does not match.");
      Assert.AreEqual(stringData, _receivedData, "String data does not match.");

      // 2.b. Let's schedule one more run of the same job
      // After job was saved, we can find it by JobName. Note that in this case JobName must be unique 
      session = context.OpenSecureSession();
      job = session.GetJobByUniqueName(ScheduledJobName2);
      _callCount = 0; 
      _receivedData = null;
      _receivedDataId = null;
      var twoHourFwd = oneHourFwd.AddHours(1);
      jobRun = JobHelper.ScheduleJobRunOn(job, runOnUtc: twoHourFwd, dataId: guidData, data: stringData);
      session.SaveChanges();
      //Move to the future and fire timers
      SetTimeOffsetFireTimers(125);
      AssertCallCount(1, "Expected scheduled call count to increment.");
      Assert.AreEqual(guidData, _receivedDataId.Value, "DataId does not match.");
      Assert.AreEqual(stringData, _receivedData, "String data does not match.");


      // 3. Let's schedule a job with initial failures; start time 10 minutes forward
      SetTime0();
      _callCount = 0;
      jobRun = JobHelper.ExecuteWithRetriesOn(session, "6. Scheduled Job, 2 fails", 
           (jobCtx) => ScheduledJob(jobCtx, 2, "456", 5), utcNow.AddMinutes(10), threadType: jobThreadType);
      job = jobRun.Job;
      session.SaveChanges();
      // After 9 minutes job should NOT be executed
      SetTimeOffsetFireTimers(9);
      // Thread.Sleep(50); //just an extra time to finish
      AssertCallCount(0, "Expected job not executed.");
      // After 12 minutes job should be executed and fail
      SetTimeOffsetFireTimers(12);
      // Thread.Sleep(100); //just an extra time to finish
      AssertCallCount(1, "Expected job to execute and fail after 12 minutes");
      jobRun = job.GetLastFinishedJobRun();
      Assert.AreEqual(JobRunStatus.Error, jobRun.Status, "Expected error status.");
      //execute 2 more times, total 3 - finally should succeed
      SetTimeOffsetFireTimers(15);
      // Thread.Sleep(100); //just an extra time to finish
      SetTimeOffsetFireTimers(18);
      // Thread.Sleep(100);
      AssertCallCount(3, "Expected job tried 3 times");
      jobRun = job.GetLastFinishedJobRun();
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected completed status.");

      // 4. Long running job 
      SetTime0();
      _callCount = 0; 
      job = JobHelper.CreateJob(session, "7. Long running job", (ctx) => LongRunningJobMethod(ctx, "xyz", 42), JobThreadType.Background);
      jobRun = job.StartJobOnSaveChanges();
      session.SaveChanges();
      Thread.Sleep(50); 
      // Job must be started 
      AssertCallCount(1, "Expected long job to start");
      EntityHelper.RefreshEntity(jobRun);
      Assert.AreEqual(JobRunStatus.Executing, jobRun.Status, "Expecte long job status Executing");
      SetTimeOffsetFireTimers(5, waitForJobFinish: false); //move forward 5 minutes, should still be executing
      EntityHelper.RefreshEntity(jobRun);
      Assert.AreEqual(JobRunStatus.Executing, jobRun.Status, "Expecte long job status Executing");
      SetTimeOffsetFireTimers(11); //move forward 11 minutes, should be completed
      EntityHelper.RefreshEntity(jobRun);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expecte long job status Completed");

      ResetTimeOffset();
    }

    static Guid? _receivedDataId;
    static string _receivedData;
    private static void ScheduledJob(JobRunContext jobContext, int failCount, string arg0, int arg1) {
      _callCount++;
      //The DataId and Data are sent as parameters to ScheduleJobRunOn call; we check here that it is passed correctly
      _receivedDataId = jobContext.DataId;
      _receivedData = jobContext.Data;
      if(jobContext.AttemptNumber <= failCount)
        Util.Throw("Job run failed; target fail count: {0}, attempt number: {0}.", failCount, jobContext.AttemptNumber);
    }

    private static void LongRunningJobMethod(JobRunContext jobContext, string arg0, int arg1) {
      _callCount++;
      var timeService = jobContext.App.TimeService;
      var startTime = timeService.UtcNow;
      var endTime = startTime.AddMinutes(10);
      var count = 0;
      while(timeService.UtcNow < endTime) {
        Thread.Sleep(1);
        jobContext.UpdateProgress(count++, "Continuing, count: " + count);
      }
      jobContext.UpdateProgress(100, "Completed successfully");
    }



    [TestMethod]
    public void TestJobExecution_CronJobs() {
      try {
        _timersControl.EnableAutoFire(false);
        TestCronJobs();
      } finally {
        _timersControl.EnableAutoFire(true);
        ResetTimeOffset();
      }
    }

    private void TestCronJobs() {
      // We use user-bound context and session (as if user is logged in), to check that retries are executed as 
      // the same user
      var context = GetUserBoundContext();
      var session = context.OpenSecureSession();

      SetTime0(); //1am today
      IJob job;
      IJobRun jobRun; 
      IJobSchedule jobSchedule;

      _callCount = 0; 
      // Schedule CRON job to run every 10 minutes
      job = session.CreateJob("8. CRON job", (ctx) => CronJobMethod(ctx, "abc", 99));
      jobSchedule = job.CreateJobSchedule("Sample Schedule",  "/10 * * * * *"); //run every 10 minutes
      session.SaveChanges();

      SetTimeOffsetFireTimers(5); // 5 minutes
      AssertCallCount(0, "Expected 0 call count.");
      jobRun = job.GetLastFinishedJobRun();
      Assert.IsNull(jobRun, "Expected no job run");

      // After 11 minutes, there must be 1 run
      SetTimeOffsetFireTimers(11);
      AssertCallCount(1, "Expected 1 call count.");
      jobRun = job.GetLastFinishedJobRun();
      Assert.IsNotNull(jobRun, "Expected 1 job run");
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed.");

      // After 15 minutes, still 1 run
      SetTimeOffsetFireTimers(15);
      AssertCallCount(1, "Expected 1 call count.");

      // After 21 minutes, 2 runs 
      SetTimeOffsetFireTimers(21);
      AssertCallCount(2, "Expected 2 call count.");
      var allRuns = session.EntitySet<IJobRun>().Where(jr => jr.Job == job).ToList();
      // 3 JobRun records - 2 completed + 1 scheduled in the future 
      Assert.AreEqual(3, allRuns.Count, "Expected 3 job run records.");

      //Disable schedule and kill pending run, to avoid disturbing other tests
      SetTime0();
      EntityHelper.RefreshEntity(jobSchedule); //to refresh NextRunId 
      jobSchedule.Status = JobScheduleStatus.Stopped;
      session.SaveChanges(); //this should stop the pending run

    }

    private static void CronJobMethod(JobRunContext jobContext, string arg0, int arg1) {
      _callCount++;
    }


    // Utilities ==========================================================================================================

    static int _callCount;
    // Helper method, to be able to stop on invalid call count
    private static void AssertCallCount(int value, string message) {
      if(_callCount == value)
        return;
 //     Debugger.Break();
      Assert.IsTrue(false, message); 
    }

    // We use sessions bound to particular user, to test that retries are executed as the same user
    private OperationContext GetUserBoundContext() {
      var session = _app.OpenSystemSession();
      var dora = session.EntitySet<IUser>().Where(u => u.UserName == "Dora").Single();
      var userInfo = new UserInfo(dora.Id, dora.UserName);
      var context = new OperationContext(_app, userInfo);
      return context; 
    }

    // We set initial time to fixed 1am current day, for easier debugging
    DateTime _timeZero;
    private void SetTime0() {
      ResetTimeOffset(); 
      var utcNow = _app.TimeService.UtcNow;
      _timeZero = utcNow.Date.AddHours(1);
      var offs = _timeZero.Subtract(utcNow);
      _app.TimeService.SetCurrentOffset(offs);
    }

    private void ResetTimeOffset() {
      _app.TimeService.SetCurrentOffset(TimeSpan.Zero);
    }

    private void FastForwardTimeFireTimers(int minutes) {
      for(int i = 0; i <= minutes; i++)
        SetTimeOffsetFireTimers(i, waitForJobFinish: true);
    }

    private void SetTimeOffsetFireTimers(int minutes, bool waitForJobFinish = true) {
      ResetTimeOffset();
      var utcNow = _app.TimeService.UtcNow;
      var targetTime = _timeZero.AddMinutes(minutes);
      var offs = targetTime.Subtract(utcNow); 
      _app.TimeService.SetCurrentOffset(offs);
      _timersControl.FireAll();
      while(!JobExecutionModule.PendingCountIsZero())
        Thread.Yield();
      if(!waitForJobFinish)
        return; 
      while(true) {
        var count = _jobInfoService.GetRunningJobs().Count;
        if(count == 0)
          return;
        Thread.Yield(); 
      }
    }

  }//class
}//ns
