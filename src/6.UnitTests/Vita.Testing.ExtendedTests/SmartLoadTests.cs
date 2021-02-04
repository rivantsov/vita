using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Tools.Testing;
using Vita.Entities.Runtime;
using System.Collections.Generic;
using BookStore;

namespace Vita.Testing.ExtendedTests {


  [TestClass]
  public class SmartLoadTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }

    private long GetSelectCount() {
      return Startup.BooksApp.AppEvents.SelectQueryCounter;
    }


    [TestMethod]
    public void TestSmartLoadFacility() {
      Startup.BooksApp.LogTestStart();
      var app = Startup.BooksApp;

      var session = app.OpenSession(EntitySessionOptions.EnableSmartLoad);
      session.LogMessage("=== Testing SmartLoad for entity references");

      var oldCount = GetSelectCount();
      var books = session.EntitySet<IBook>().ToList();
      // go thru the list and touch book.Publisher.Name; the first touch should trigger single query
      //  retrieving all publishers for books in the list. So total query count should be 2: one for books
      //  and one for publishers
      var names = new List<string>();
      foreach (var bk in books) {
        names.Add(bk.Publisher.Name);
        // names.Add(bk.Editor?.UserName); // you can try the same with bk.Editor; the diff is that Editor is nullable
      }
      Assert.IsTrue(names.Count > 1, "Expected some names");
      var qryCount = GetSelectCount() - oldCount;
      Assert.AreEqual(2, qryCount, "expected 2 queries"); // books, publishers

      // Lists and nested lists/refs
      session = app.OpenSession(EntitySessionOptions.EnableSmartLoad);
      session.LogMessage("=== Testing SmartLoad for lists and deep-nested child refs and lists");
      names.Clear();

      oldCount = GetSelectCount();
      // Load publishers, then go thru the list, go thru publisher's books, for each book go thru authors, for each
      // author touch the user
      //  there should be 4 queries: publishers, books, authors, users
      var pubs = session.EntitySet<IPublisher>().ToList();
      foreach (var pub in pubs)
        foreach (var bk in pub.Books)
          foreach (var auth in bk.Authors)
            if (auth.User != null)
              names.Add(auth.User.UserName);
      var numQueries = GetSelectCount() - oldCount;
      Assert.AreEqual(4, numQueries, "Expected 4 queries");

    }//method

  }//class

}
