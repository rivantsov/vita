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
  public class JobRunnerTests {

    static string SuccessMessage = "Completed successfully!";
    EntityApp _app;
    Guid _userId; 
    ITimerServiceControl _timersControl;

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp(); 
      _app = Startup.BooksApp;
      _timersControl = _app.GetService<ITimerServiceControl>();
    }

    [TestMethod]
    public void TestJobExecution_ImmediateJobs () {
      try {
        TestUtil.EnableTimers(_app, false); 
        _timersControl.EnableAutoFire(false); //disable firing automatically, only thru FireAll
        AsyncHelper.RunSync(()=> TestImmediateJobs());
      } finally {
        ResetTimeOffset();
        TestUtil.EnableTimers(_app, true);
      }
    }

    public async Task TestImmediateJobs () {
      _app.TimeService.SetCurrentOffset(TimeSpan.Zero);
      // Use session associated with particular user. Jobs are retried with the same user context as original attempt. 
      var session = _app.OpenSystemSession();
      var dora = session.EntitySet<IUser>().Where(u => u.UserName == "dora").Single();
      var userInfo = new UserInfo(dora.Id, dora.UserName);
      var context = new OperationContext(_app, userInfo);

      session = context.OpenSecureSession(); 

      // We use this arg to test deserialization of arguments
      var listArg = new List<string>() { "a", "b", "c", "d", "e", "f" };
      // Set explicitly the default RetryPolicy
      var jobExecConfig = _app.GetConfig<JobModuleSettings>();
      jobExecConfig.DefaultRetryPolicy = new RetryPolicy(new[] { 2, 2, 2, 2, 2, 2 }); //repeat 6 times with 2 minute intervals
      
      JobRunContext jobRunContext;
      IJobRun jobRun;  
      // 1.a Async job executing successfully 1st time     -------------------------------------------
      jobRunContext = await JobHelper.ExecuteWithRetriesAsync(session.Context, "1a: Sample async job, no failures", 
                                  (ctx) => JobRunnerTests.JobMethodAsync(ctx, 0, "Some string argument", listArg));
      Assert.AreEqual(JobRunStatus.Completed, jobRunContext.Status, "Expected Completed for async job"); 
      // Check that job was not persisted - because it ended successfully first time
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNull(jobRun, "Expected async job succeed and never be persisted.");

      // 1.b Async job initially failing     -------------------------------------------
      jobRunContext = await JobHelper.ExecuteWithRetriesAsync(session.Context, "1b: Sample async job, 3 failures",
                                  (ctx) => JobRunnerTests.JobMethodAsync(ctx, 3, "Some string argument", listArg));
      Assert.AreEqual(JobRunStatus.Error, jobRunContext.Status, "Expected Error");
      // Check that job is persisted - because it ended with error
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNotNull(jobRun, "Expected JobRun record in db.");
      //fast-forward time by 10 minutes and fire timers on the way; 
      FastForwardTimeFireTimers(10);
      // RetryPolicy states to repeat with 2 minutes intervals, 
      // so after 3 failures final run should succeed
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed");
      //attemptNumber is 1-based
      Assert.AreEqual(4, jobRun.AttemptNumber, "Wrong attempt number for successful run.");
      ResetTimeOffset();

      // 2.a NoWait job executing successfully 1st time     -------------------------------------------
      jobRunContext = JobHelper.ExecuteWithRetriesNoWait(session.Context, "2a: Sample no-wait job, no failures",
                                  (ctx) => JobRunnerTests.JobMethod(ctx, 0, "Some string argument", listArg));
      Thread.Sleep(100); //make sure job finished
      Assert.AreEqual(JobRunStatus.Completed, jobRunContext.Status, "Expected completed status");
      // Check that job was not persisted - because it ended successfully first time
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNull(jobRun, "Expected no-wait job succeed and never be persisted.");

      // 2.b NoWait job failing initially     -------------------------------------------
      jobRunContext = JobHelper.ExecuteWithRetriesNoWait(session.Context, "2b: Sample no-wait job, 3 failures",
                                  (ctx) => JobRunnerTests.JobMethod(ctx, 3, "Some string argument", listArg));
      Thread.Sleep(100); //make sure job finished
      Assert.AreEqual(JobRunStatus.Error, jobRunContext.Status, "Expected Error status");
      // Check that job is persisted - because it failed
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.IsNotNull(jobRun, "Expected no-wait jobRun record in db.");
      //fast-forward time by 10 minutes; 
      FastForwardTimeFireTimers(10);
      // RetryPolicy states to repeat with 2 minutes intervals, 
      // so after 3 failures final run should succeed
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed");
      //attemptNumber is 1-based
      Assert.AreEqual(4, jobRun.AttemptNumber, "Wrong attempt number for successful run.");
      ResetTimeOffset();

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
      Assert.IsTrue(jobRun2 == jobRun , "Expected the same job run");
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected Completed status.");

      // 3.b Job executed on SaveChanges, executed with initial failures ------------------------------------
      jobRun = JobHelper.ExecuteWithRetriesOnSaveChanges(session, "3b: Sample on-save job, 3 failures",
                        (ctx) => JobMethod(ctx, 3, "Sample string arg", listArg), dataId, data, threadType);
      session.SaveChanges();
      Thread.Sleep(200); //make sure job finished (with error)
      // get this failed run
      jobRun = jobRun.Job.GetLastFinishedJobRun();
      Assert.AreEqual(JobRunStatus.Error, jobRun.Status, "Expected Error status.");
      //fast-forward time by 10 minutes; 
      FastForwardTimeFireTimers(10);
      jobRun = session.GetLastFinishedJobRun(jobRunContext.JobId);
      Assert.AreEqual(JobRunStatus.Completed, jobRun.Status, "Expected status Completed");
      //attemptNumber is 1-based
      Assert.AreEqual(4, jobRun.AttemptNumber, "Wrong attempt number for successful run.");
      ResetTimeOffset();


    } //method

    // Job methods --------------------------------------------------------------------------------------
    static int _callCount; 
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

    private void ResetTimeOffset() {
      _app.TimeService.SetCurrentOffset(TimeSpan.Zero);
    }
    private void FastForwardTimeFireTimers(int minutes) {
      for(int i = 0; i <= minutes; i++) {
        _app.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(i * 2));
        _timersControl.FireAll();
        Thread.Sleep(10);
      }
    }

    [TestMethod]
    public void TestJobExecution_ScheduledJobs() {
      try {
        _timersControl.EnableAutoFire(false);
        TestScheduledJob();
      } finally {
        _timersControl.EnableAutoFire(true); 
        _app.TimeService.SetCurrentOffset(TimeSpan.Zero);
      }
    }

    private void TestScheduledJob() {
      var session = _app.OpenSystemSession();
      var utcNow = _app.TimeService.UtcNow;
      var oneHourFwd = utcNow.AddHours(1);
      const string ScheduledJobName = "Sample Scheduled Job";
      // Create job - start time 'OnDemand'
      var job = JobHelper.CreateJob(session, ScheduledJobName, (jobCtx) => ScheduledJob(jobCtx, "abc", 10));

      // Now invoke the job at certain time. For each invocation we can provide some custom data; 
      // pass custom Guid and string; this data is available inside job method in jobRunContext.Data, DataId 
      _scheduledCallCount = 0;
      var guidData = Guid.NewGuid();
      var stringData = "SomeString";
      var jobRun = JobHelper.ScheduleJobRunOn(job, runOnUtc: oneHourFwd, dataId: guidData, data: stringData);
      session.SaveChanges();

      _app.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(30));       // Move forwad 30 minutes - job should not fire yet
      _timersControl.FireAll();
      Thread.Sleep(10);
      Assert.AreEqual(0, _scheduledCallCount, "Expected scheduled job not activated.");

      _app.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(61));       // Move forwad
      _timersControl.FireAll(); 
      Thread.Sleep(10);
      Assert.AreEqual(1, _scheduledCallCount, "Expected scheduled call count to increment.");
      Assert.AreEqual(guidData, _receivedDataId.Value, "DataId does not match.");
      Assert.AreEqual(stringData, _receivedData, "String data does not match.");

      // After job was saved, we can find it by JobName. Note that in this case JobName must be unique 
      job = session.GetJobByUniqueName(ScheduledJobName); 
      _scheduledCallCount = 0;
      _receivedData = null;
      _receivedDataId = null;
      var twoHourFwd = oneHourFwd.AddHours(1); 
      session = _app.OpenSystemSession();
      var jobRun2 = JobHelper.ScheduleJobRunOn(job, runOnUtc: twoHourFwd, dataId: guidData, data: stringData);
      session.SaveChanges(); 
      //Move to the future and fire timers
      _app.TimeService.SetCurrentOffset(TimeSpan.FromMinutes(121)); 
      _timersControl.FireAll();
      Thread.Sleep(10);
      Assert.AreEqual(1, _scheduledCallCount, "Expected scheduled call count to increment.");
      Assert.AreEqual(guidData, _receivedDataId.Value, "DataId does not match.");
      Assert.AreEqual(stringData, _receivedData, "String data does not match.");

    }

    static int _scheduledCallCount; 
    static Guid? _receivedDataId;
    static string _receivedData;
    private static void ScheduledJob(JobRunContext context, string arg0, int arg1) {
      _scheduledCallCount++; 
      _receivedDataId = context.DataId;
      _receivedData = context.Data; 
    }

  }//class
}//ns
