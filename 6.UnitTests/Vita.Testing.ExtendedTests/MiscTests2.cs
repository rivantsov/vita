using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Data.Driver;
using Vita.Samples.BookStore;
using Vita.Modules.Login;
using Vita.Data.Upgrades;
using Vita.Data;
using Vita.Tools.Testing;

namespace Vita.Testing.ExtendedTests {

  // Not real tests, simple demos. disabled for now
  public partial class MiscTests {

    [TestMethod]
    public void TestBugFixes() {

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false);
      var utcNow = app.TimeService.UtcNow;

      // Bug - Any was not working properly
      var csBook = session.EntitySet<IBook>().Where(b => b.Title.StartsWith("c#")).First();
      // Any with parameter (filter)
      var hasReviews1 = session.EntitySet<IBookReview>().Any(br => br.Book == csBook);
      // Debug.WriteLine("SQL: " + session.GetLastCommand().CommandText);
      Assert.IsTrue(hasReviews1, "Expected c# has reviews");
      // Any without parameter
      var hasReviews2 = session.EntitySet<IBookReview>().Where(br => br.Book == csBook).Any();
      // Debug.WriteLine("SQL: " + session.GetLastCommand().CommandText);
      Assert.IsTrue(hasReviews2, "Expected c# has reviews");

      // Bug - Count(filter) argument expr causes error (should be translated to Where cond)
      var b1 = session.EntitySet<IBook>().FirstOrDefault(b => b.Price > 1);
      var cntf = session.EntitySet<IBook>().Count(b => b.Price > 1);
      var cnt = session.EntitySet<IBook>().Count();
      // count with grouping and filter
      var bkGroups = session.EntitySet<IBook>().GroupBy(b => b.Category)
        .Select(g => new { g.Key, Count = g.Count(b => b.Price > 1) })
        .ToList();

      // bug: SQLite does not format properly Datetime parameters in LINQ
      var bk1 = session.EntitySet<IBook>().First(); //get random book 
      var bkId = bk1.Id;
      var createdOn1 = bk1.CreatedOn.AddSeconds(-1);
      var createdOn2 = bk1.CreatedOn.AddSeconds(1);
      var bk2 = session.EntitySet<IBook>()
        .Where(b => b.Id == bkId && b.CreatedOn > createdOn1 && b.CreatedOn < createdOn2)
        .Where(b => b.CreatedOn > new DateTime(2017, 1, 1)) //this results in constant (quoted string) in SQL
        .FirstOrDefault();
      var cmd = session.GetLastCommand();
      var cmdStr = cmd.ToLogString(); 
      //Obviously it should bring the same book 
      Assert.IsNotNull(bk2, "Expected book");
      Assert.IsTrue(bk2 == bk1, "Expected the same book");

      //Bug ConvertHelper.ChangeType fails to convert string->enum, null-> double?
      var bkEd = BookEdition.Paperback | BookEdition.EBook;
      var strBkEd = bkEd.ToString();
      var bkEd2 = (BookEdition) ConvertHelper.ChangeType(strBkEd, typeof(BookEdition));
      Assert.AreEqual(bkEd, bkEd2, "Enum ChangeType failed.");
      // null -> float?
      var nullFl = ConvertHelper.ChangeType(string.Empty, typeof(float?));
      var oneFl = (float?) ConvertHelper.ChangeType("1.0", typeof(float?));
      Assert.IsNull(nullFl, "Expected null for float? type.");
      Assert.IsTrue(Math.Abs(oneFl.Value - 1.0) < 0.001, "Expected 1.0 as float value");


      //Bug: selecting from nullable property resulted in conversion error
      var editorIds = session.EntitySet<IBook>().Select(b => b.Editor.Id).ToList();
      Assert.IsTrue(editorIds.Count > 0, "Expected an editor");
      var editors = session.EntitySet<IBook>().Select(b => b.Editor).ToList();
      Assert.IsTrue(editors.Count > 0, "Expected an editor");
      //Check how back property of IUser.EditedBooks works
      var editedBooks = editors[0].BooksEdited;
      Assert.IsTrue(editedBooks.Count > 0, "Failed to read User.EditedBooks property");
      //check with First(), this is slightly different path in code, extra conversion
      var firstEditorId = session.EntitySet<IBook>().Where(b => b.Editor != null).Select(b => b.Editor.Id).First();
      Assert.IsTrue(firstEditorId != Guid.Empty, "Expected first editor id");

      //testing fix - if list is explicitly typed as IList<Guid>, it was failing
      IList<Guid> pubIds = session.EntitySet<IPublisher>().Select(p => p.Id).ToList();
      var booksByAllPubs = session.EntitySet<IBook>().Where(b => pubIds.Contains(b.Publisher.Id)).ToList();
      Assert.IsTrue(booksByAllPubs.Count > 0, "Expected all books.");

      //bug: entity list property based on nullable reference (property user.EditedBooks, with book.Editor nullable property)
      var qEditorsOfProgBooks = session.EntitySet<IUser>()
              .Where(u => u.BooksEdited.Any(b => b.Category == BookCategory.Programming));
      var editorsOfProgBooks = qEditorsOfProgBooks.ToList();
      Assert.IsTrue(editorsOfProgBooks.Count > 0, "Expected some editors of programming books");

      // Count + LIMIT is not supported by any server; check that appropriate error message is thrown
      session = app.OpenSession();
      session.EnableCache(false);
      var query = session.EntitySet<IBook>().Skip(1).Take(1);
      int count;
      var exc = TestUtil.ExpectFailWith<Exception>(() => count = query.Count());
      Assert.IsTrue(exc.Message.Contains("does not support COUNT"));

      //Test for bug fix - loading DateTime? property from SQLite db
      var allBooks = session.GetEntities<IBook>();
      var bkNotPublished = allBooks.FirstOrDefault(bk => bk.PublishedOn == null);
      Assert.IsNotNull(bkNotPublished, "Failed to find not published book.");

      // Test that using entity-type parameter with null value works
        IUser nullUser = null;
        var authorsNonUsers = session.EntitySet<IAuthor>().Where(a => a.User == nullUser).ToList();
        Assert.IsTrue(authorsNonUsers.Count > 0, "Failed to find non users");

      //Query by null in nullable column - find not published book; there must be IronMan comic there.
      var qBooksNotPublished = from b in allBooks
                               where b.PublishedOn == null
                               select b;
      var lstBooksNotPublished = qBooksNotPublished.ToList();
      Assert.IsTrue(lstBooksNotPublished.Count > 0, "Failed to find not published book.");

      //Testing that SQLite DbCommand is disconnected - this releases server-side resources
      // Disabling this test - it does not work if you have connection pooling (pooling=true in conn string)
      //if(Startup.ServerType == DbServerType.SQLite) 
       // TryMoveSqliteFile(); 

      //Test new/delete entity - this was a bug
      session = app.OpenSession();
      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var newOrd = session.NewOrder(dora);
      session.DeleteEntity(newOrd);
      session.SaveChanges();

    }

    //Testing that SQLite DbCommand is disconnected - this releases server-side resources
    // Note: does not work if connection pooling enabled (pooling = true in conn string)
    private void TryMoveSqliteFile() {
      if(Startup.ServerType != DbServerType.SQLite)
        return; 
      var app = Startup.BooksApp;
      app.Flush(); //force flushing all logs, so no more background saves
      var currSession = app.OpenSession(); 
      var fileName = "VitaBooksSQLite.db";
      var xfileName = "X_VitaBooksSQLite.db";
      Assert.IsTrue(System.IO.File.Exists(fileName));
      if(File.Exists(xfileName)) {
        currSession.LogMessage($"Deleting {xfileName} file...");
        File.Delete(xfileName); //in case file is left from previous failure
      }
      // When tests are run in console mode, the following line fails - so far failed to find the cause; in TestExpl everything runs OK
      currSession.LogMessage($"Moving db file...");

      app.Flush();
      // !!! it blows up HERE in console mode
      File.Move(fileName, xfileName); //using Move to rename the file
      currSession = app.OpenSession();

      currSession.LogMessage($"Moving db file back...");
      File.Move(xfileName, fileName); //rename back
      currSession = app.OpenSession();
      var newUser = currSession.NewUser("testUser", UserType.Customer);
      currSession.SaveChanges();
      currSession.LogMessage($"Step 2: renaming db file...");
      File.Move(fileName, xfileName); //rename
      currSession.LogMessage($"Step 2: renaming back...");
      File.Move(xfileName, fileName); //rename back
    }

    [TestMethod]
    public void TestDbViews() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false);
      if(Startup.ServerType == DbServerType.Postgres) {
        //Postgres requires to manually refresh materialized views
        session.ExecuteNonQuery(@"Refresh materialized view ""books"".""vBookSales_mat"";");
        //session.ExecuteNonQuery(@"Refresh materialized view ""books"".""vAuthorUser"";");
      }

      //Using GetAll
      var bookSales = session.GetEntities<IBookSales>();
      Assert.IsTrue(bookSales.Count > 0, "Expected book sales records");
      //Using Linq
      var msQuery = from bs in session.EntitySet<IBookSales>()
                    where bs.Publisher == "MS Books"
                    select bs;
      var msList = msQuery.ToList();
      Assert.IsTrue(msList.Count > 0, "Expected MS Books sales.");

      //Another view IFictionBook
      var fictBooks = session.GetEntities<IFictionBook>();
      Assert.IsTrue(fictBooks.Count > 0, "Expected fiction books");
      //Using LINQ
      var firstFictBook = session.EntitySet<IFictionBook>().Where(b => b.Price > 0).FirstOrDefault();
      Assert.IsNotNull(firstFictBook, "Expected fiction book.");

      //AuthorUser view
      var authUsersList = session.EntitySet<IAuthorUser>().ToList();
      Assert.IsTrue(authUsersList.Count > 0, "Expected author/user entities");
    }


    [TestMethod]
    public void TestDbModelCompare() {

      var app = Startup.BooksApp;
      //Once we started tests, db model is updated; now if we compare db model with entity model, there should be no changes
      // We test here how well the DbModelComparer works - it should not signal any false positives (find differences when there are none)
      var ds = app.GetDefaultDataSource();
      var upgradeMgr = new DbUpgradeManager(ds.Database, app.ActivationLog);
      var upgradeInfo = upgradeMgr.BuildUpgradeInfo(); //.AddDbModelChanges(currentDbModel, modelInDb, DbUpgradeOptions.Default, app.ActivationLog);
      var changeCount = upgradeInfo.TableChanges.Count + upgradeInfo.NonTableChanges.Count;
      #region Rant about Postgres
      // For Postgres we have no way to compare view definitions to detect change. 
      // The view SQL returned by information_schema.Views is extensively modified and beautified (!) version of 
      // the original View SQL (these guys must be very proud, I'm happy for them). 
      // So views in PG driver will always be marked as modified, unless you use extra upgradeMode parameter in RegisterView.
      #endregion
      if(Startup.ServerType == DbServerType.Postgres) {
        var viewCount = app.Model.Entities.Where(e => e.Kind == Entities.Model.EntityKind.View).Count(); 
        changeCount -= viewCount; // views are marked as mismatch, so ignore these
      }
      if (changeCount > 0) {
        // sometimes randomly fails, trying to catch it
        var changes = string.Join(Environment.NewLine, upgradeInfo.AllScripts.Select(s => s.Sql).ToList());
        Debug.WriteLine("\r\nFAILED DbModelCompare test, update scripts: ====================================================\r\n" 
           + changes);
        //Debugger.Break();  
      }
      Assert.AreEqual(0, changeCount, "Expected no changes");
    }

    [TestMethod]
    public void TestPasswordHashers() {
      //run it only for MS SQL, to avoid slowing down console run for all servers
      if(Startup.ServerType != DbServerType.MsSql)
        return;
      
      IPasswordHasher hasher;
      var salt = Guid.NewGuid().ToByteArray();
      var pwd = "MyPassword_*&^";
      long start, timeMs;
      bool match;
      string hash;

      // You can use this test to approximate the 'difficulty' of hashing algorithm for your computer. 
      //  It prints the time it took to hash the pasword. This time should not be too low, desirably no less than 100 ms.
      hasher = new BCryptPasswordHasher(workFactor: 10); //each +1 doubles the effort; on my machine: 10 -> 125ms, 11->242ms
      start = Util.GetPreciseMilliseconds();
      hash = hasher.HashPassword(pwd, salt);
      timeMs = Util.GetPreciseMilliseconds() - start;
      match = hasher.VerifyPassword(pwd, salt, hasher.WorkFactor, hash);
      Assert.IsTrue(match, "BCrypt hasher failed.");
      Debug.WriteLine("BCrypt hasher time, ms: " + timeMs);

      hasher = new Pbkdf2PasswordHasher(iterationCount: 2000); // on my machine: 2000-> 13ms, 5000->32ms
      start = Util.GetPreciseMilliseconds();
      hash = hasher.HashPassword(pwd, salt);
      timeMs = Util.GetPreciseMilliseconds() - start;
      match = hasher.VerifyPassword(pwd, salt, hasher.WorkFactor, hash);
      Assert.IsTrue(match, "Pbkdf hasher failed.");
      Debug.WriteLine("Pbkdf hasher time, ms: " + timeMs);
    }


  }//class
}
