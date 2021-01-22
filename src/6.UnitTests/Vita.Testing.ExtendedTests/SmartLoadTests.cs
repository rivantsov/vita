using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Tools.Testing;
using Vita.Samples.BookStore;
using Vita.Entities.Runtime;

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
      
      var oldCount = GetSelectCount(); 
      var books = session.EntitySet<IBook>().ToList();
      foreach(var bk in books) {
        var pubName = bk.Publisher.Name;
      }
      var qryCount = GetSelectCount() - oldCount;
      Assert.AreEqual(2, qryCount, "expected 2 queries"); // books, publishers
    }//method


  }//class
}
