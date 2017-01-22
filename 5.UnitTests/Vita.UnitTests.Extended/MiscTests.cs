using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Diagnostics;

using Vita.Entities;
using Vita.Data.Driver;
using Vita.Modules.Login;

using Vita.Samples.BookStore;
using Vita.UnitTests.Common;
using Vita.Modules.Login.GoogleAuthenticator;

namespace Vita.UnitTests.Extended {

  [TestClass]
  public partial class MiscTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown(); 
    }

    // Not exactly a test, just generates a few authenticator passcodes for current time. 
    [TestMethod]
    public void TestGoogleAuthenticator() {
      var secretBase32 = "ABCDABCD33ABCDABCD44";
      var secret = Base32Encoder.Decode(secretBase32); 
      var appIdentity = "VitaBookStore";
      var current = GoogleAuthenticatorUtil.GetCurrentCounter();
      for (int i = -1; i <= 4; i++) {
        var passcode = GoogleAuthenticatorUtil.GeneratePasscode(secret, current + i);
        Trace.WriteLine("Passcode: " + passcode);
      }
      //Paste this URL into browser to see QR image
      var url = GoogleAuthenticatorUtil.GetQRUrl(appIdentity, secretBase32);
      Trace.WriteLine("QR URL:   " + url);
      //
      Trace.WriteLine("Key for phone app, manual entry:   " + secretBase32);
    }

    [TestMethod]
    public void TestValidation() {
      var app = Startup.BooksApp; 
      var session = app.OpenSession();
      // Entity validation: trying to save entities with errors -----------------------------------------
      var invalidAuthor = session.NewAuthor("First", 
        "VeryLoooooooooooooooooooooooooooongLaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaastName"); //make it more than 50
      var invalidBook = session.NewBook(BookEdition.EBook, BookCategory.Fiction, "Not valid book", "Some invalid book", null, DateTime.Now, -5.0m);
      //We expect 3 errors: Author's last name is too long; Publisher cannot be null;
      // Price must be > 1 cent. The first 3 errors are found by built-in validation; the last error, price check, is added
      // by custom validation method.
      var cfEx = TestUtil.ExpectClientFault(() => {session.SaveChanges();}); 
      Assert.AreEqual(3, cfEx.Faults.Count, "Wrong # of faults.");
      foreach (var fault in cfEx.Faults)
        Assert.IsTrue(!string.IsNullOrWhiteSpace(fault.Message), "Fault message is empty");
    }

    [TestMethod]
    public void TestCanDelete() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // create some book to delete
      var msPub = session.EntitySet<IPublisher>().First(p => p.Name.StartsWith("MS"));
      var fsBook = session.NewBook(BookEdition.Paperback, BookCategory.Programming, "F# programming", "Introduction to F#", msPub, DateTime.Now, 20);
      var john = session.EntitySet<IAuthor>().First(a => a.LastName == "Sharp");
      fsBook.Authors.Add(john); 
      session.SaveChanges();
      var fsBkId = fsBook.Id;

      // Test CanDelete. There are no orders for Fs book, so we may be able to delete it
      // We can delete a book even if there are links for it in BookAuthor link table (BookAuthor.Book has CascadeDelete attr)
      // so the result here should be true
      session = app.OpenSession();
      fsBook = session.GetEntity<IBook>(fsBkId); 
      Type[] blockingEntities;
      var canDelete = session.CanDeleteEntity(fsBook, out blockingEntities);
      Assert.IsTrue(canDelete, "CanDelete F# book failed.");
      session.DeleteEntity(fsBook);
      session.SaveChanges();

      //check it is deleted; we also check here that entity cache works correctly and does not keep object.
      fsBook = session.GetEntity<IBook>(fsBkId);
      Assert.IsNull(fsBook, "F# not deleted.");
      var allBooks = session.GetEntities<IBook>(); 

      //Now check Publisher: mspub has associated books, so it should not be allowed to be deleted.
      msPub = session.EntitySet<IPublisher>().Single(p => p.Name.StartsWith("MS"));
      canDelete = session.CanDeleteEntity(msPub, out blockingEntities);
      Assert.IsFalse(canDelete, "Error: MS publisher should not be allowed to be deleted.");
      Assert.IsTrue(blockingEntities != null && blockingEntities.Length > 0, "Blocking entities empty.");
      //Still try to delete
      session.DeleteEntity(msPub);
      session.LogMessage("\r\n-- TEST: expecting integrity violation exception ...."); //to make clear in Log that exc is expected
      var dex = TestUtil.ExpectDataAccessException(() => { session.SaveChanges(); });
      Assert.AreEqual(DataAccessException.SubTypeIntegrityViolation, dex.SubType, "Expected integrity violation exception subtype.");

      //Bug fix: CanDelete blows up in secure session
      var lindaTheEditor = session.EntitySet<IUser>().First(u => u.UserName == "Linda").ToUserInfo();
      var opContext = new OperationContext(Startup.BooksApp, lindaTheEditor);
      var secSession = opContext.OpenSecureSession();
      msPub = secSession.EntitySet<IPublisher>().Single(p => p.Name.StartsWith("MS"));
      canDelete = secSession.CanDeleteEntity(msPub, out blockingEntities);
      Assert.IsFalse(canDelete, "Expected CanDelete to be false.");

    }

    [TestMethod]
    public void TestEntityCache() {
      if (!Startup.CacheEnabled)
        return;
      var app = Startup.BooksApp;
      IEntitySession session; 
      // Books catalog (books, authors, publishers) is kept in FULL entity cache - entire tables are held in memory
      // Book orders, order lines are cached in sparse cache - only single records are kept for limited time (30 seconds).


      // 1. EntityCache 
      session = app.OpenSession();
      var bk = session.EntitySet<IBook>().First(); //this will start reloading cache
      Thread.Sleep(500); // let cache reload
      session = app.OpenSession(); //open another session
      bk = session.EntitySet<IBook>().First(); //now it should come from cache
      var bkRec = EntityHelper.GetRecord(bk);
      Assert.AreEqual(CacheType.FullSet, bkRec.SourceCacheType, "Entity cache test failed: book is not cached.");

      // Sparse cache
      // Remember that records in sparse cache expire in 30 seconds, 
      // so if you step through in debugger and stop for too long, the test might fail.
      session = app.OpenSession();
      var orderId = session.EntitySet<IBookOrder>().First().Id;
      // Open session and get order by Id - this will place order entity into sparse cache
      session = app.OpenSession();
      var order1 = session.GetEntity<IBookOrder>(orderId);

      // Open new session and read order again - it should come from sparse cache
      session = app.OpenSession();
      var order2 = session.GetEntity<IBookOrder>(orderId);
      var orderRec = EntityHelper.GetRecord(order2);
      Assert.AreEqual(CacheType.Sparse, orderRec.SourceCacheType, "Sparse cache test failed: order record is not cached in sparse cache.");

      // Testing deleting from sparse entity cache
      session = app.OpenSession();
      var billy = session.NewUser("billy", UserType.Customer);
      var billyId = billy.Id; 
      session.SaveChanges();

      // billy should be cached in sparse cache, get it and check it's coming from cache
      session = app.OpenSession();
      billy = session.GetEntity<IUser>(billyId);
      var billyRec = EntityHelper.GetRecord(billy);
      Assert.AreEqual(CacheType.Sparse, billyRec.SourceCacheType, "Billy rec is not delivered from cache.");
      //Now try to delete billy - make sure it does not stay in cache after being deleted in the database
      session.DeleteEntity(billy);
      session.SaveChanges(); 

      // read it back, make sure it is expelled from sparse cache
      session = app.OpenSession();
      billy = session.GetEntity<IUser>(billyId);
      Assert.IsNull(billy, "Billy is not deleted.");
    }


    [TestMethod]
    public void TestEntityToString() {
      // Entity.ToString() is determined by Display attribute
      var session = Startup.BooksApp.OpenSession();

      var johnSharp = session.EntitySet<IAuthor>().First(u => u.LastName == "Sharp");
      Assert.AreEqual("Author: Sharp, John", johnSharp.ToString());

      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var doraOrder1 = session.EntitySet<IBookOrder>().Where(o => o.User == dora).OrderBy(o => o.CreatedOn).First();
      //Order.ToString() is implemented using custom method referenced in Display attribute
      var expected = string.Format("Dora, {0} items.", doraOrder1.Lines.Count);
      Assert.AreEqual(expected, doraOrder1.ToString());

      // Book's Display attribute includes reference to 'Publisher.Name' - testing this dotted reference
      var csBook = session.EntitySet<IBook>().First(b => b.Title.StartsWith("c#"));
      Assert.AreEqual("c# Programming, Paperback, EBook, MS Books", csBook.ToString());
    }

    [TestMethod]
    public void TestPasswordStrengthChecker() {
      var app = Startup.BooksApp;
      var loginStt = Startup.BooksApp.GetConfig<LoginModuleSettings>(); 
      var checker = loginStt.PasswordChecker;

      // Basics
      var strength = checker.Evaluate("klmn");
      Assert.AreEqual(PasswordStrength.Unacceptable, strength);
      strength = checker.Evaluate("klmnop");
      Assert.AreEqual(PasswordStrength.Weak, strength);
      strength = checker.Evaluate("klmnop71");
      Assert.AreEqual(PasswordStrength.Medium, strength);
      strength = checker.Evaluate("klMNop71!#");
      Assert.AreEqual(PasswordStrength.Strong, strength);
    }

    [TestMethod]
    public void TestGetEntitiesByIdArray() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      // Thread.Sleep(100); //to let the cache reload
      // disable cache just for testing; in general it should work in cache and in db
      session.EnableCache(false); 

      var books = session.GetEntities<IBook>(take: 3);
      var bookIds = books.Select(b => b.Id).ToList();
      //Now select by array of IDs
      var booksByIds = session.GetEntities<IBook>(bookIds);
      Assert.AreEqual(books.Count, booksByIds.Count, "Expected same book count.");
      foreach (var id in bookIds)
        Assert.IsNotNull(booksByIds.FirstOrDefault(b => b.Id == id), "Expected to find book by ID");
      // use empty list
      bookIds.Clear();
      var noBooksByIds = session.GetEntities<IBook>(bookIds);
      Assert.AreEqual(0, noBooksByIds.Count, "Expected no books");
    }



  }//class
}
