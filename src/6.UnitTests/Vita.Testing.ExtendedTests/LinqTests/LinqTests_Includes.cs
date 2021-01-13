using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Samples.BookStore;
using Vita.Tools.Testing; 

namespace Vita.Testing.ExtendedTests {

  public partial class LinqTests {

    [TestMethod] 
    public void TestLinqInclude() {
      Startup.BooksApp.LogTestStart();

      try {
        //DisplayAttribute.Disabled = true;   // just for internal debugging, to disable automatic loading of entities for Display
        Startup.BooksApp.AppEvents.ExecutedSelect += AppEvents_ExecutedSelect; //to spy after executed selects
        //using (SetupHelper.BooksApp.LoggingApp.Suspend()) { //to suspend logging activity in debugging
          TestLinqIncludeImpl(); 
        // }
      } finally {
        DisplayAttribute.Disabled = false;
        Startup.BooksApp.AppEvents.ExecutedSelect -= AppEvents_ExecutedSelect;
      }
    }

    int _loadCount;
    void AppEvents_ExecutedSelect(object sender, EntityCommandEventArgs e) {
      _loadCount++;
    }//method


    private void TestLinqIncludeImpl() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var summary = new List<string>();

      // chained properties, with multiple expressions in auto object
      var qReviews = session.EntitySet<IBookReview>().Where(r => r.Rating >= 1)
        .Include(r => new { r.User, r.Book.Publisher }); // will load users, referenced books AND its publishers
      var reviews = qReviews.ToList();
      //Go through all reviews, touch properties of objects; 
      // Without Include, accessing 'review.Book.Title' would cause loading of IBook entity; with Include there should be no extra db roundrips
      // We handle ExecutedSelect app event (spying on the app); the event fired for any record loaded; we  increment loadCount in event handler
      _loadCount = 0;
      summary.Clear();
      foreach (var rv in reviews) 
        summary.Add(string.Format(" User {0} reviewed '{1}' from {2} and gave rating: {3}", rv.User.DisplayName, rv.Book.Title, rv.Book.Publisher.Name, rv.Rating));
      Assert.IsTrue(summary.Count > 0, "Expected some summaries.");
      var txtSummary = string.Join(Environment.NewLine, summary);
      Assert.AreEqual(0, _loadCount, "Expected 0 load count");
      Startup.BooksApp.Flush();

      // list properties, many2one and many2many; 2 forms of Include 
      var qOrders = session.EntitySet<IBookOrder>().Where(o => o.Status != OrderStatus.Canceled)
        .Include(o => new { o.Lines, o.User })   // include version 1 - against results of query (IBookOrder)
        .Include((IBookOrderLine l) => l.Book) // include version 2 - against 'internal' results, order lines; means 'whenever we load IBookOrderLine, load related Book'
        .Include((IBook b) => new { b.Publisher, b.Authors}) // for any book, load publisher and authors
        ;
      session.LogMessage("Query with multiple includes: ==================================");
      var orders = qOrders.ToList();
      session.LogMessage("----- query executed. Starting iterating over records");

      //verify - go thru all orders, touch lines, books, users, publishers; check there were no DB commands executed
      _loadCount = 0;
      summary.Clear(); 
      foreach (var order in orders)
        foreach (var ol in order.Lines) {
          var authors = string.Join(", ", ol.Book.Authors.Select(a => a.FullName));
          summary.Add(string.Format(" User {0} bought {1} books titled '{2}' by {3} from {4}.", 
               order.User.UserName, ol.Quantity, ol.Book.Title, authors, ol.Book.Publisher.Name));
        }
      session.LogMessage("---- Completed iterating over records ---------------- ");
      txtSummary = string.Join(Environment.NewLine, summary);
      Assert.IsTrue(summary.Count > 0, "Expected non-empty summary");
      Assert.AreEqual(0, _loadCount, "Expected no extra db load commands.");

      //Using nullable property
      session = app.OpenSession(); 
      var qBooks = session.EntitySet<IBook>().Where(b => b.Price > 0)
        .Include(b => b.Editor); //book.Editor is nullable property
      var books = qBooks.ToList();
      _loadCount = 0; 
      summary.Clear(); 
      foreach (var bk in books) {
        if (bk.Editor != null)
          summary.Add(string.Format("Book '{0}' edited by {1}.", bk.Title, bk.Editor.UserName));
      }
      txtSummary = string.Join(Environment.NewLine, summary);
      Assert.AreEqual(0, _loadCount, "Expected zero load count.");
      //var txtLog = session.Context.LocalLog.GetAllAsText(); 

      //single entity
      session = app.OpenSession(); 
      var someBook = session.EntitySet<IBook>().Where(b => b.Price > 0)
                  .Include(b => b.Publisher).First();
      Assert.IsNotNull(someBook, "Expected a book.");
      _loadCount = 0;
      var pubName = someBook.Publisher.Name;
      Assert.AreEqual(0, _loadCount, "Expected no load commands.");

      // for queries that do not return entities the include is simply ignored
      var bkGroups = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id).Include(b => b.Key).ToList();
      Assert.IsNotNull(bkGroups, "Expected something here");


      // Reviews query, but now using Context.AddInclude;
      var context = app.CreateSystemContext();
      context.AddInclude((IBookOrder o) => new { o.Lines, o.User })
             .AddInclude((IBookOrderLine l) => l.Book.Publisher);
      session = context.OpenSession(); 
      var qOrders2 = session.EntitySet<IBookOrder>().Where(o => o.Status != OrderStatus.Canceled);
      var orders2 = qOrders2.ToList();
      //verify - go thru all orders, touch lines, books, users, publishers; check there were no DB commands executed
      _loadCount = 0;
      summary.Clear();
      foreach (var order in orders2)
        foreach (var ol in order.Lines)
          summary.Add(string.Format(" User {0} bought {1} books titled '{2}' from {3}.", order.User.UserName, ol.Quantity, ol.Book.Title, ol.Book.Publisher.Name));
      txtSummary = string.Join(Environment.NewLine, summary);
      Assert.IsTrue(summary.Count > 0, "Expected non-empty summary");
      Assert.AreEqual(0, _loadCount, "Expected no extra db load commands.");

      var removed = context.RemoveInclude((IBookOrder o) => new { o.Lines, o.User }) && context.RemoveInclude((IBookOrderLine l) => l.Book.Publisher);
      Assert.IsTrue(removed, "Removing Include from operation context failed.");
      Assert.IsFalse(context.HasIncludes(), "Removing Include from operation context failed.");

      //Search with includes; you can use a single include with ExecuteSearch. If you have more, use Context.AddInclude
      session = app.OpenSession();
      var searchParams = new SearchParams() {OrderBy = "CreatedOn-desc", Take = 10};
      var where = session.NewPredicate<IBookReview>()
        .And(r => r.Book.Category == BookCategory.Programming);
      var foundReviews = session.ExecuteSearch(where, searchParams, include: r => new { r.Book.Publisher, r.User });
      Assert.IsTrue(foundReviews.Results.Count > 0, "Expected some reviews");
      _loadCount = 0;
      summary.Clear();
      foreach (var rv in foundReviews.Results)
        summary.Add(string.Format(" User {0} reviewed '{1}' from {2} and gave rating: {3}", rv.User.DisplayName, rv.Book.Title, rv.Book.Publisher.Name, rv.Rating));
      txtSummary = string.Join(Environment.NewLine, summary);
      Assert.AreEqual(0, _loadCount, "Expected 0 load count");

    }


  }//class

}
