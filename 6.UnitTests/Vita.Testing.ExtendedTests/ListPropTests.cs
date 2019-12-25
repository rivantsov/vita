using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Tools.Testing;
using Vita.Samples.BookStore;

namespace Vita.Testing.ExtendedTests {


  [TestClass]
  public class ListPropTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }

    [TestMethod]
    public void TestListProperties() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp; 
      // Get some IDs
      var session = app.OpenSession(); 
      var csBook = session.EntitySet<IBook>().Single(b => b.Title.StartsWith("c#"));
      var csBookId = csBook.Id;
      var msPubId = csBook.Publisher.Id;
      var ironManBook = session.EntitySet<IBook>().Single(b => b.Title.StartsWith("Iron"));
      var kidPubId = ironManBook.Publisher.Id;

      // Entity lists, one-to-many -----------------------------------------------------------------------
      // For a publisher, get the books (Publisher.Books)
      session = app.OpenSession();
      var msPub = session.GetEntity<IPublisher>(msPubId);
      //eventually getting wrong number here, trying to catch it
      if(msPub.Books.Count != 3)  
        System.Diagnostics.Debugger.Break(); // Found it: F# book shows up - it is created and deleted in other test; entity cache is not invalidated on time
      Assert.AreEqual(3, msPub.Books.Count, "Invalid # of books from MS Books");
      foreach (var bk in msPub.Books)
        Assert.IsTrue(!string.IsNullOrWhiteSpace(bk.Title), "Book title is empty.");  

      //Entity lists, many-to-many -----------------------------------------------------------------------
      // Loading a book (about c#) and enumerating its Authors property.
      session = app.OpenSession();
      csBook = session.GetEntity<IBook>(csBookId);
      session.LogMessage("!!!! about to read book.Authors property.");
      Assert.IsTrue(2 == csBook.Authors.Count, "Invalid authors count for c# book");
      foreach (var a in csBook.Authors)
        Assert.IsTrue(!string.IsNullOrWhiteSpace(a.FullName), "Author full name is empty."); 
      //remove one author, save and check if it is removed
      var a1 = csBook.Authors[0];
      csBook.Authors.Remove(a1);
      session.SaveChanges();

      session = app.OpenSession();
      csBook = session.GetEntity<IBook>(csBookId);
      var csAuthors = csBook.Authors;
      Assert.IsTrue(1 == csAuthors.Count, "Author is not removed for c# book");
      //Add author back, we gonna need him
      a1 = session.GetEntity<IAuthor>(a1.Id);
      csBook.Authors.Add(a1);
      session.SaveChanges(); 

      // Remove/add with many-to-many lists
      session = app.OpenSession();
      csBook = session.GetEntity<IBook>(csBookId);
      var jack = (from a in csBook.Authors
                 where a.FirstName == "Jack"
                 select a).FirstOrDefault();
      csBook.Authors.Remove(jack);
      csBook.Authors.Add(jack);
      session.SaveChanges(); //we just verify that it works

    }//method

    [TestMethod] 
    public void TestPersistOrderInAttr() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      // Find order with 2 lines
      // The following query with Count does not work for SQL CE - it does not support subqueries (of certain kinds)
      // var doraOrder = session.EntitySet<IBookOrder>().First(o => o.User.UserName == "Dora" && o.Lines.Count >= 2); 
      // So using this instead
      var doraOrder = session.EntitySet<IBookOrder>()
        .Where(o => o.User.UserName == "Dora")
        .ToList().First(o => o.Lines.Count >= 2); 
      var lines = doraOrder.Lines;
      var item0 = lines[0];
      var item1 = lines[1];

      //Change line ordering; 
      lines[0] = item1;
      lines[1] = item0;
      session.SaveChanges();
      // IBookOrderLine has [PersistOrderIn("LineNumber")] attribute, so changed order in the list
      // should be reflected in LineNumber property (after save); and if we read lines back, they should come in new order
      Assert.AreEqual(lines[0].LineNumber, 1, "Ordering not applied.");
      Assert.AreEqual(lines[1].LineNumber, 2, "Ordering not applied.");
      //Open fresh session, read lines back and check order
      session = app.OpenSession();
      doraOrder = session.GetEntity<IBookOrder>(doraOrder.Id); //get fresh copy
      Assert.AreEqual(doraOrder.Lines[0].Id, item1.Id, "Explicit ordering failed!");
      Assert.AreEqual(doraOrder.Lines[1].Id, item0.Id, "Explicit ordering failed!");
    }


  }//class
}
