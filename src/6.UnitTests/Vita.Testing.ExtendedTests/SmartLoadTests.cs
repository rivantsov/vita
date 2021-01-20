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

    [TestMethod]
    public void TestSmartLoadBasics() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp; 
      // Get some IDs
      var session =  app.OpenSession(EntitySessionOptions.EnableSmartLoad);
      var books = session.EntitySet<IBook>().ToList(); 
      foreach(var bk in books) {
        var pubName = bk.Publisher.Name;
      }
    }//method


  }//class
}
