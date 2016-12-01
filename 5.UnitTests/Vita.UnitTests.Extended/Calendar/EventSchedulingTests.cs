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
using Vita.Modules.Calendar;
using Vita.Samples.BookStore;


namespace Vita.UnitTests.Extended {
  [TestClass]
  public class EventSchedulingTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }

    [TestMethod]
    public void TestScheduledEvents() {
      try {
        Startup.BooksApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
        TestScheduledEventsImpl(); 
      } finally {
        // cleanup/reenable global services
        Startup.BooksApp.TimeService.SetCurrentOffset(TimeSpan.Zero);
        Startup.BooksApp.GetService<ITimerServiceControl>().EnableAutoFire(true); 
      }
    }

    // Let's say we want to do a 60-minutes Podcast 'New Programming books', every Wed at 2 pm  
    // 24 hours before the event (Tue 2pm) we send anouncement emails to customers (invitations)
    // 10 minutes before the event we send reminders (podcast about to start)
    // 10 minutes after the event (70 minutes after start) we send emails asking people for feedback.
    // Let's create these events/schedules/jobs. 
    // Note: we don't actually do anything in associated jobs, just set the flag in _podcastStatus field signalling that 
    //  job method had been invoked. 
    // Calendar scheduling works in UTC, so we use UTC everywhere, as if we are in London

    private void TestScheduledEventsImpl() {
      //schedule weekly Podcast event New Programming Books
      var timeService = Startup.BooksApp.TimeService;
      var timersControl = Startup.BooksApp.GetService<ITimerServiceControl>();
      var jobService = Startup.BooksApp.GetService<IJobExecutionService>();
      // To avoid depending on time when test run, we set current time explicitly
      // let's set current datetime to nearest Fri, 1 pm; we will schedule podcast to happen every Wednesday at 14:00
      var today = timeService.UtcNow.Date;
      while(today.DayOfWeek != DayOfWeek.Friday)
        today = today.AddDays(1);
      SetCurrentDateTime(today.AddHours(13)); // 13:00 on Fri 

      var session = Startup.BooksApp.OpenSession();
      // Podcast jobs to run
      var invitateJob = jobService.CreateBackgroundJob(session, "NewBooksPodcast-Invitation", (jobCtx) => NewBooksPodcast_SendInvitations(jobCtx), JobFlags.None);
      var remindJob = jobService.CreateBackgroundJob(session, "NewBooksPodcast-Reminder", (jobCtx) => NewBooksPodcast_SendReminders(jobCtx), JobFlags.None);
      var startPodcastJob = jobService.CreateBackgroundJob(session, "NewBooksPodcast-Start", (jobCtx) => NewBooksPodcast_Start(jobCtx), JobFlags.None);
      var askFeedbackJob = jobService.CreateBackgroundJob(session, "NewBooksPodcast-AskFeedback", (jobCtx) => NewBooksPodcast_AskFeedback(jobCtx), JobFlags.None);
      // Events: create main event and associated events
      var podcastTemplate = session.NewEventTemplate("Podcast-NewProgrBooks", "Podcast - New Programming Books", "Podcast - presentation of new programming books.",
         durationMinutes: 60, jobToRun: startPodcastJob);
      podcastTemplate.AddSubEvent("SendInvitations", "Send Podcast Invitations", -24 * 60, invitateJob); //run send-invitations job 24 hours before main event
      podcastTemplate.AddSubEvent("SendReminders", "Send Podcast starting emails", -10, remindJob);  // 10 minutes before
      podcastTemplate.AddSubEvent("AskFeedback", "Send emails asking for feedback", 70, askFeedbackJob); // 70 minutes after the main event start (10 minutes after end)
      // Schedule podcast using CRON spec
      var podcastSchedule = podcastTemplate.CreateSchedule("0 14 * * Wed *"); // 14:00
      session.SaveChanges();

      // now let's check how events are activated if we run thru time. We shift the current time and force firing timer events. 
      timersControl.EnableAutoFire(false);
      _podcastStatus = PodcastStatus.None;
      timersControl.FireAll(); //fire today, Fri - nothing should happen
      Thread.Sleep(50);
      Assert.AreEqual(PodcastStatus.None, _podcastStatus, "Expected no events activated.");
    }


    // flags to indicate executed steps in podcast event and subevents
    [Flags]
    enum PodcastStatus {
      None = 0,
      SentInvites = 1,
      SentReminders = 1 << 1,
      Started = 1 << 2,
      Completed = 1 << 3,  
      SentAskFeedback = 1 << 4,
    }

    static PodcastStatus _podcastStatus; 

    private static void NewBooksPodcast_SendInvitations(JobRunContext jobContext) {
      _podcastStatus |= PodcastStatus.SentInvites;
    }

    private static void NewBooksPodcast_SendReminders(JobRunContext jobContext) {
      _podcastStatus |= PodcastStatus.SentReminders;
    }
    private static void NewBooksPodcast_Start(JobRunContext jobContext) {
      _podcastStatus |= PodcastStatus.Started;
      Thread.Sleep(100);
      _podcastStatus |= PodcastStatus.Completed; 
    }
    private static void NewBooksPodcast_AskFeedback(JobRunContext jobContext) {
      _podcastStatus |= PodcastStatus.SentAskFeedback;

    }


    // Utilities 
    private void SetCurrentDateTime(DateTime dt) {
      var timeService = Startup.BooksApp.TimeService;
      timeService.SetCurrentOffset(dt.Subtract(timeService.UtcNow));
    }

  }//class
}//ns
