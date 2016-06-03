using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.DataHistory;
using Vita.Samples.BookStore;

namespace Vita.UnitTests.Extended {

  [TestClass]
  public class DataHistoryTests {

    [TestInitialize]
    public void TestInit() {
      SetupHelper.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      SetupHelper.TearDown();
    }

    [TestMethod]
    public void TestDataHistory() {
      var app = SetupHelper.BooksApp;
      try {
        //Book Review has history tracking enabled; let's create a review, update it a few times, and then delete it
        // we set 'current time' explicitly, to separate events in time, for proper sorting by date
        app.TimeService.SetCurrentOffset(TimeSpan.FromHours(-24));
        var session = app.OpenSession();
        var ferb = session.EntitySet<IUser>().Where(u => u.UserName == "Ferb").First();
        // let's associate session/context with Ferb, so his UserId is recorded in history
        session.Context.User = new UserInfo(ferb.Id, ferb.UserName);
        var winBook = session.EntitySet<IBook>().Where(b => b.Title.StartsWith("Windows")).First();
        var ferbReview = session.NewReview(ferb, winBook, 2, "Very expensive!", "Expensive; starting first chapters.");
        var r0 = ferbReview.Review;
        session.SaveChanges();
        //Let's update it
        app.TimeService.SetCurrentOffset(TimeSpan.FromHours(-12));
        ferbReview.Rating = 1;
        ferbReview.Review = "Very difficult to go through, code samples are not clear";
        var r1 = ferbReview.Review;
        session.SaveChanges();
        // Update it again
        app.TimeService.SetCurrentOffset(TimeSpan.FromHours(-6));
        ferbReview.Rating = 4;
        ferbReview.Review = "First chapters are hard, but then it starts make sense.";
        var r2 = ferbReview.Review;
        session.SaveChanges();
        // and again
        app.TimeService.SetCurrentOffset(TimeSpan.FromHours(-1));
        ferbReview.Rating = 5;
        ferbReview.Caption = "Excellent!";
        ferbReview.Review = "Excellent - was well worth the price.";
        var r3 = ferbReview.Review;
        session.SaveChanges();
        // finally let's delete it 
        app.TimeService.SetCurrentOffset(TimeSpan.Zero);
        session.DeleteEntity(ferbReview);
        session.SaveChanges();
        // Now let's get history of changes
        var histService = app.GetService<IDataHistoryService>();
        var reviewHist = histService.GetEntityHistory(session, typeof(IBookReview), ferbReview.Id);
        Assert.AreEqual(5, reviewHist.Count, "Expected 5 history entries"); // create, 3 updates, delete
        // history is by datetime-descending, so #0 is DELETE
        Assert.AreEqual(HistoryAction.Deleted, reviewHist[0].HistoryEntry.Action, "Expected delete action.");
        Assert.AreEqual(HistoryAction.Updated, reviewHist[1].HistoryEntry.Action, "Expected update action.");
        Assert.AreEqual(HistoryAction.Created, reviewHist[4].HistoryEntry.Action, "Expected Create action.");
        //Let's check review text
        Assert.AreEqual(r0, reviewHist[4].Values["Review"], "Review does not match.");
        Assert.AreEqual(r1, reviewHist[3].Values["Review"], "Review does not match.");
        Assert.AreEqual(r2, reviewHist[2].Values["Review"], "Review does not match.");
        Assert.AreEqual(r3, reviewHist[1].Values["Review"], "Review does not match.");
        Assert.AreEqual(r3, reviewHist[0].Values["Review"], "Review does not match."); //it is delete, the text should be the same as last update

        // a single history entry, at point in time - between 1st and second update; it is 9 hours ago; review should be r1
        var dt9hAgo = app.TimeService.UtcNow.AddHours(-9);
        var review9hAgo = histService.GetEntityOnDate(session, typeof(IBookReview), ferbReview.Id, dt9hAgo);
        Assert.AreEqual(r1, review9hAgo.Values["Review"], "Review history 9h ago does not match expected.");

      } finally {
        // in case we fail with exception
        app.TimeService.SetCurrentOffset(TimeSpan.Zero);
      }
    }//method

  }//class
}
