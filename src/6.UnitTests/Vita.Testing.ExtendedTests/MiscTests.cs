using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Diagnostics;

using Vita.Entities;
using Vita.Modules.Login;

using BookStore;
using Vita.Modules.Login.GoogleAuthenticator;
using Vita.Tools.Testing;

namespace Vita.Testing.ExtendedTests {

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
      Startup.BooksApp.LogTestStart();

      var secretBase32 = "ABCDABCD33ABCDABCD44";
      var secret = Base32Encoder.Decode(secretBase32);
      var appIdentity = "VitaBookStore";
      var current = GoogleAuthenticatorUtil.GetCurrentCounter();
      for (int i = -1; i <= 4; i++) {
        var passcode = GoogleAuthenticatorUtil.GeneratePasscode(secret, current + i);
        Trace.WriteLine("Google auth passcode: " + passcode);
      }
      //Paste this URL into browser to see QR image
      var url = GoogleAuthenticatorUtil.GetQRUrl(appIdentity, secretBase32);
      Trace.WriteLine("QR URL:   " + url);
      //
      Trace.WriteLine("Key for phone app, manual entry:   " + secretBase32);
    }

    [TestMethod]
    public void TestValidation() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      // Entity validation: trying to save entities with errors -----------------------------------------
      var invalidAuthor = session.NewAuthor("First",
        "VeryLoooooooooooooooooooooooooooongLaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaastName"); //make it more than 50
      var invalidBook = session.NewBook(BookEdition.EBook, BookCategory.Fiction, "Not valid book", "Some invalid book", null, DateTime.Now, -5.0m);
      //We expect 3 errors: Author's last name is too long; Publisher cannot be null;
      // Price must be > 1 cent. The first 3 errors are found by built-in validation; the last error, price check, is added
      // by custom validation method.
      var cfEx = TestUtil.ExpectClientFault(() => { session.SaveChanges(); });
      Assert.AreEqual(3, cfEx.Faults.Count, "Wrong # of faults.");
      foreach (var fault in cfEx.Faults)
        Assert.IsTrue(!string.IsNullOrWhiteSpace(fault.Message), "Fault message is empty");
    }

    [TestMethod]
    public void TestCanDelete() {
      Startup.BooksApp.LogTestStart();

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
      // Note: auth is not ported to 2.0 (net core)
      var lindaTheEditor = session.EntitySet<IUser>().First(u => u.UserName == "Linda").ToUserInfo();
      var opContext = new OperationContext(Startup.BooksApp, lindaTheEditor);
      var secSession = opContext.OpenSecureSession();
      msPub = secSession.EntitySet<IPublisher>().Single(p => p.Name.StartsWith("MS"));
      canDelete = secSession.CanDeleteEntity(msPub, out blockingEntities);
      Assert.IsFalse(canDelete, "Expected CanDelete to be false.");
    }


    [TestMethod]
    public void TestDbRefIntegrityErrors() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // Try deleting entity when there are still refs from other entities
      var daExc = TestUtil.ExpectDataAccessException(() => {
        // msPub has some books, so deleting it will fail
        var msPub = session.EntitySet<IPublisher>().First(p => p.Name.StartsWith("MS"));
        session.DeleteEntity(msPub);
        session.SaveChanges();
      });
      Assert.AreEqual(DataAccessException.SubTypeIntegrityViolation, daExc.SubType);
     
      // Try assigning ref to non-existent entity (with unknown PK)
      daExc = TestUtil.ExpectDataAccessException(() => {
        var csBook = session.EntitySet<IBook>().First(b => b.Title.StartsWith("c#"));
        csBook.Publisher =  session.GetEntity<IPublisher>(Guid.NewGuid(), LoadFlags.Stub); //creates a stub
        session.SaveChanges();
      });
      Assert.AreEqual(DataAccessException.SubTypeIntegrityViolation, daExc.SubType);
    }

    // Investigating reported issue #202: PropagateUpdatedOn does not trigger when adding new line
    // Dec 2022 - so far no problem detected, works as expected without fixes

    [TestMethod]
    public void TestBugPropagateUpdatedOnAttr() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // IBookOrderLine has [PropagateUpdateOn] on Order property. 
      // For an existing order, after we add/modify/delete any line, the parent Order's status should be Modified, and UpdatedOn field should be updated 
      //  on SaveChanges.

      // We create order with one book; save it; and then add another book - the BookOrder should be set to Modified
      var ferb = session.EntitySet<IUser>().First(u => u.UserName == "Ferb");
      var order = session.NewOrder(ferb);
      var csBook = session.EntitySet<IBook>().First(b => b.Title.StartsWith("c#"));
      order.Add(csBook, 1);
      session.SaveChanges();

      var orderRec = EntityHelper.GetRecord(order);
      Assert.AreEqual(EntityStatus.Loaded, orderRec.Status);
      var updOn = order.UpdatedOn;
      Thread.Sleep(100); // to make difference in UpdatedOn noticeable

      // now lets add another book and check the order status
      var vbBook = session.EntitySet<IBook>().First(b => b.Title.StartsWith("VB"));
      order.Add(vbBook, 1);
      // Check that order state changed to modified
      Assert.AreEqual(EntityStatus.Modified, orderRec.Status, "Expected Order status changed to Modified");
      session.SaveChanges();
      var newUpdOn = order.UpdatedOn;
      Assert.IsTrue(newUpdOn > updOn.AddMilliseconds(50), "Expected later new UpdatedOn value");
    }


    [TestMethod]
    public void TestEntityToString() {
      Startup.BooksApp.LogTestStart();

      // Entity.ToString() is determined by Display attribute
      var session = Startup.BooksApp.OpenSession();

      var johnSharp = session.EntitySet<IAuthor>().First(u => u.LastName == "Sharp");
      Assert.AreEqual("Author: Sharp, John", johnSharp.ToString());

      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var doraOrder1 = session.EntitySet<IBookOrder>().Where(o => o.User == dora).OrderBy(o => o.CreatedOn).First();
      //Order.ToString() is implemented using custom method referenced in Display attribute
      var expected = $"Order by Dora, {doraOrder1.Lines.Count} items.";
      Assert.AreEqual(expected, doraOrder1.ToString());

      // Book's Display attribute includes reference to 'Publisher.Name' - testing this dotted reference
      var csBook = session.EntitySet<IBook>().First(b => b.Title.StartsWith("c#"));
      Assert.AreEqual("c# Programming, Paperback, EBook, MS Books", csBook.ToString());
    }

    [TestMethod]
    public void TestPasswordStrengthChecker() {
      Startup.BooksApp.LogTestStart();

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


  }//class

}
