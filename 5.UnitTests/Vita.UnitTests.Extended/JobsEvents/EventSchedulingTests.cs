using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading; 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vita.UnitTests.Common;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.JobExecution;


namespace Vita.UnitTests.Extended {

  [TestClass]
  public class EventSchedulingTests {
    ITimeService _timeService;
    ITimerServiceControl _timersControl;
    IJobInformationService _jobService; 

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }

    //Temporarily disabled
    //  [TestMethod]
    public void TestEventScheduling () {
      // !!!! if this test fails, try to adjust wait time in FireAllAndPause method (increase it); 
      // it might be that on your machine threads compete differently, and work on bkground thread does not finish on time
      _timeService = Startup.BooksApp.TimeService;
      _timersControl = Startup.BooksApp.GetService<ITimerServiceControl>();
      _jobService = Startup.BooksApp.GetService<IJobInformationService>();
      _timersControl.EnableAutoFire(false);
      FireAllTimers(); //make first fire, so that next fire does not think it restarts
      Startup.BooksApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
      try {
        TestSchedulingRepeatedEvent();
        TestSchedulingSingleEvent();
      } finally {
        // cleanup/reenable global services
        _timeService.SetCurrentOffset(TimeSpan.Zero);
        _timersControl.EnableAutoFire(true); 
      }
    }

    private void TestSchedulingSingleEvent() {
      //Set current time at noon
      var today = _timeService.UtcNow.Date;
      var noon = today.AddHours(12);
      SetCurrentDateTime(noon); 
      var session = Startup.BooksApp.OpenSession();
      //Create event at 1pm
      var sampleEvt = session.CreateEvent(noon.AddHours(1), "SampleEvent", "Sample event.", "Sample event.",
           (jobCtx) => RunSampleEvent(jobCtx, "abcd"));
      session.SaveChanges();
      // Fire immediately, event should not be activated
      _argValue = null;
      FireAllTimers();
      Thread.Sleep(20);
      Assert.AreEqual(null, _argValue, "Expected argValue null.");
      // shift time to 1pm, fire timers, check that RunSampleEvent method was called
      SetCurrentDateTime(noon.AddHours(1));
      FireAllTimers();
      Assert.AreEqual("abcd", _argValue, "Expected arg value to be set.");
      EntityHelper.RefreshEntity(sampleEvt);
      Assert.AreEqual(EventStatus.Completed, sampleEvt.Status, "Expected completed status.");

      // fire again later, make sure event not fired
      _argValue = null; 
      SetCurrentDateTime(noon.AddHours(1).AddMinutes(1));
      FireAllTimers();
      Assert.AreEqual(null, _argValue, "Expected argValue null.");
    }

    private static string _argValue;
    private static void RunSampleEvent(JobRunContext jobContext, string arg) {
      _argValue = arg;
      //Event ID should be available from JobContext; we can read event entity here
      // So we can reuse a job for multiple events - each will get its own event ID
      var eventId = jobContext.EventId;
    }

    private void TestSchedulingRepeatedEvent() {
      // Let's schedule an event 'cleanup db', twice a week, on Thursday and Sunday at 3 am.
      // We set fixed start date, Nov 1, 2016; and then move time forward and check that events are fired at proper time
      _timeService.SetCurrentOffset(TimeSpan.Zero);
      var startDate = new DateTime(2016, 11, 1); //Tue Nov 1, 2016
      SetCurrentDateTime(startDate); 
      var session = Startup.BooksApp.OpenSession();
      var cleanupJob = _jobService.CreateBackgroundJob(session, "TestWeeklyCleanup", (jobCtx) => ExecuteWeeklyDbCleanup(jobCtx, "123"));
      var cleanupEventInfo = session.NewEventInfo("TestWeeklyCleanup", "Weekly DB cleanup", "Cleans up db weekly", jobToRun: cleanupJob);
      var schedule = cleanupEventInfo.CreateSchedule("00 3 * * Sun,Thu *"); //every Sun, Wed at 3 am
      session.SaveChanges();
      // The first run should be scheduled at Thursday, Nov 3 at 3 am
      Assert.IsNotNull(schedule.NextStartOn, "Expected NextStartOn.");
      var nov3_3am = startDate.AddDays(2).AddHours(3);
      Assert.AreEqual<DateTime>(nov3_3am, schedule.NextStartOn.Value, "Expected Nov 3, 3am");
      // move current time to nov3, fire event
      _cleanupCount = 0;
      SetCurrentDateTime(nov3_3am);
      Thread.Sleep(50);
      FireAllTimers();
      Assert.AreEqual(1, _cleanupCount, "Expected cleanup count to increment.");
      //check next date in schedule, should be sunday Nov 6
      EntityHelper.RefreshEntity(schedule);
      var nov6_Sun_3am = nov3_3am.AddDays(3);
      Assert.AreEqual<DateTime>(nov6_Sun_3am, schedule.NextStartOn.Value, "Expected Nov 6 Sund, 3am");
      //let's move to Sat, fire events - nothing should happen, count should stay at 1
      var nov5_Sat = nov3_3am.AddDays(2);
      SetCurrentDateTime(nov5_Sat);
      FireAllTimers();
      Assert.AreEqual(1, _cleanupCount, "Expected cleanup count to remain 1.");

      // move to Nov 6 Sun, 4am; we are past 1 hour (system was down); we check that cleanup scheduled for 3 am still will be fired
      Thread.Sleep(100); 
      var nov6_Sun_4am = nov6_Sun_3am.AddHours(1);
      SetCurrentDateTime(nov6_Sun_4am);
      FireAllTimers();
      Assert.AreEqual(2, _cleanupCount, "Expected cleanup count 2 - missed Sun event not fired! .");
      //check next StarOn in schedule - should be Nov 10
      var nov10_Thu_3am = nov3_3am.AddDays(7);
      EntityHelper.RefreshEntity(schedule);
      Assert.AreEqual(nov10_Thu_3am, schedule.NextStartOn, "Expected next Thu Nov 10, 3 am as next start.");

      //disable the schedule, to avoid impact on other tests (annoying in debugger)
      schedule.Status = ScheduleStatus.Stopped;
      session.SaveChanges(); 
    }

    static int _cleanupCount; 
    private static void ExecuteWeeklyDbCleanup(JobRunContext jobContext, string arg) {
      _cleanupCount++; 
    }

    private void FireAllTimers() {
      Thread.Sleep(50); // to finish all prior events
      Thread.Yield(); 
      _timersControl.FireAll();
      // event fired by Timers.OneMinute tick launches job on background thread
      Thread.Yield();
      Thread.Sleep(50);
      // somehow we need to sleep twice here, to allow background thread to complete actual work 
      Thread.Yield();
      Thread.Sleep(50);
    }

    // Utilities 
    private void SetCurrentDateTime(DateTime dt) {
      var utcNow = DateTime.UtcNow; 
      var timeService = Startup.BooksApp.TimeService;
      timeService.SetCurrentOffset(dt.Subtract(utcNow));
    }

  }//class
}//ns
