using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Tools.Testing;
using Vita.Samples.BookStore;
using Vita.Entities.Runtime;
using System.Collections.Generic;

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
      return Startup.BooksApp.AppEvents.SelectCount;
    }


    [TestMethod]
    public void TestSmartLoadFacility() {
      Startup.BooksApp.LogTestStart();
      var app = Startup.BooksApp;

      var session =  app.OpenSession(EntitySessionOptions.EnableSmartLoad);
      session.LogMessage("=== Testing SmartLoad for entity references");

      var oldCount = GetSelectCount(); 
      var books = session.EntitySet<IBook>().ToList();
      // go thru the list and touch book.Publisher.Name; the first touch should trigger single query
      //  retrieving all publishers for books in the list. So total query count should be 2: one for books
      //  and one for publishers
      var allPubNames = new List<string>(); 
      foreach(var bk in books) {
        allPubNames.Add(bk.Publisher.Name);
      }
      Assert.IsTrue(allPubNames.Count > 1, "Expected some publisher names");
      var qryCount = GetSelectCount() - oldCount;
      Assert.AreEqual(2, qryCount, "expected 2 queries"); // books, publishers
    }//method


  }//class
}
