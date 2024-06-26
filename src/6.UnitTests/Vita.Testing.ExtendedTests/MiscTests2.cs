﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Upgrades;
using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Tools.Testing;
using BookStore;

namespace Vita.Testing.ExtendedTests; 

// Not real tests, simple demos. disabled for now
public partial class MiscTests {

  [TestMethod]
  public void TestBugFixes() {
    Startup.BooksApp.LogTestStart();

    var app = Startup.BooksApp;
    var session = app.OpenSession();
    var utcNow = app.TimeService.UtcNow;
    IDbCommand cmd;

    // Incorrect impl of Group By createTime.Date
    var grpByQry = session.EntitySet<IBookOrder>().GroupBy(bo => bo.CreatedOn.Date)
      .Select(ig => new { Date = ig.Key, Count = ig.Count() });
    var grpBy = grpByQry.ToList();
    cmd = session.GetLastCommand();
    Debug.WriteLine(cmd.CommandText);  // This is incorrect SQL!
    // workaround
    var preQry = session.EntitySet<IBookOrder>().Select(bo => new { Date = bo.CreatedOn.Date, bo.Status });
    var grpBy2 = preQry.GroupBy(bo => new { bo.Status, bo.Date })
      .Select(ig => new { Status = ig.Key.Status, Date = ig.Key.Date, Count = ig.Count()}).ToList();
    cmd = session.GetLastCommand();
    Debug.WriteLine(cmd.CommandText);

    // Bug - incorrect linq grouping of expressions with chained Where conditions
    // https://github.com/rivantsov/vita/issues/93
    var bks = session.EntitySet<IBook>()
               .Where(b => b.Price > 0 || b.Price < 0)
               .Where(b => b.Price < -1 || b.Price < -2)
               .ToList();
    var sql = session.GetLastCommand().CommandText;
    // correct WHERE: (b.Price > 0 OR b.Price < 0) AND (b.Price < -1 OR b.Price < -2)    -- returns 0 records
    // old verion, bug-affected WHERE: 
    //                 b.Price > 0 OR b.Price < 0 AND b.Price < -1 OR b.Price < -2   -- returns all records
    Assert.AreEqual(0, bks.Count, "Bug 93 fix failed: query returned non-zero records.");


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
      .Where(b => b.Id == bkId)
      .Where(b => b.CreatedOn > createdOn1 && b.CreatedOn < createdOn2)
      .Where(b => b.CreatedOn > new DateTime(2017, 1, 1)) //this results in constant (quoted string) in SQL
      .FirstOrDefault();
    cmd = session.GetLastCommand();
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
    Startup.BooksApp.LogTestStart();

    var app = Startup.BooksApp;
    var session = app.OpenSession();
    if(Startup.ServerType == DbServerType.Postgres) {
      //Postgres requires to manually refresh materialized views
      session.ExecuteNonQuery($@"Refresh materialized view ""books"".""{BooksModule.BookSalesMatViewName}"";");
      //session.ExecuteNonQuery(@"Refresh materialized view ""books"".""vAuthorUser"";");
    }

    Trace.WriteLine("Materialized view, GetEntities");
    var bookSales1 = session.GetEntities<IBookSalesMat>();
    Assert.IsTrue(bookSales1.Count > 0, "Expected book sales records");
    foreach (var bk in bookSales1)
      Trace.WriteLine($"  Book '{bk.Title}', Copies sold: {bk.Count}, Total: {bk.Total}");

    Trace.WriteLine("Materialized view, LINQ + auto-obj");
    var matViewQuery = from bs in session.EntitySet<IBookSalesMat>()
                       select new { Title = bs.Title, Count = bs.Count, Total = bs.Total };
    var bookSales2 = matViewQuery.ToList();
    Assert.IsTrue(bookSales2.Count > 0, "Expected MS Books sales.");
    foreach (var bk in bookSales2) 
      Trace.WriteLine($"  Book '{bk.Title}', Copies sold: {bk.Count}, Total: {bk.Total}");

    // test for bug fix - Count and Total are NULL for a book without sales (IronMan book)
    Trace.WriteLine("Regular view, GetEntities");
    var bookSales3 = session.GetEntities<IBookSales>();
    Assert.IsTrue(bookSales3.Count > 0, "Expected rows in BookSales2");
    foreach (var bk in bookSales3)
      Trace.WriteLine($"  Book '{bk.Title}', Copies sold: {bk.Count}, Total: {bk.Total}");


    //Another view vFictionBooks
    Trace.WriteLine("Regular view FictionBooks, GetEntities");
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
    Startup.BooksApp.LogTestStart();
    var app = Startup.BooksApp;
    //Once we started tests, db model is updated; now if we compare db model with entity model, there should be no changes
    // We test here how well the DbModelComparer works - it should not signal any false positives (find differences when there are none)
    var ds = app.GetDefaultDataSource();
    var upgradeMgr = new DbUpgradeManager(ds.Database, app.ActivationLog);
    var upgradeInfo = upgradeMgr.BuildUpgradeInfo(); //.AddDbModelChanges(currentDbModel, modelInDb, DbUpgradeOptions.Default, app.ActivationLog);
    var changeCount = upgradeInfo.TableChanges.Count + upgradeInfo.NonTableChanges.Count;
    #region Postgres
    // For Postgres we have no way to compare view definitions to detect change. 
    // The view SQL returned by information_schema.Views is extensively modified and beautified (!) version of 
    // the original View SQL (these guys must be very proud, I'm happy for them). 
    // So views in PG driver will always be marked as modified, unless you use extra upgradeMode parameter in RegisterView.
    // Update Sept 2021. ----------------------------
    //  Introduced a workaround. If you use DbInfoModule to track db version in a separate table, then the system
    //  saves MD5 hashes of all generated views in the version record (blob field Values); on upgrade, we compare new hash
    //  for a view with old hash; if they match the view did not change. So disabling this correction clause for this test.
    //if(Startup.ServerType == DbServerType.Postgres) {
    //  var viewCount = app.Model.Entities.Where(e => e.Kind == Entities.Model.EntityKind.View).Count(); 
    //  changeCount -= viewCount; // views are marked as mismatch, so ignore these
    //}
    #endregion

    //Known issue in v3.0, SQLite in Release mode - fails, one diff detected, FK_BookOrder_EncryptedData
    if(changeCount == 1 && Startup.ServerType == DbServerType.SQLite)
      Assert.Fail("Known issue: SQLite fails in Relase mode in DbModelCompare test. Fails to load FK BookOrder->EncrData");
    //  is not found in database
    if (changeCount > 0) {
      // sometimes randomly fails, trying to catch it
      var changes = string.Join(Environment.NewLine, upgradeInfo.AllScripts.Select(s => s.Sql).ToList());
      Trace.WriteLine(//"\r\nFAILED DbModelCompare test, update scripts: ====================================================\r\n" +
         changes);
      //Debugger.Break();  
    }
    Assert.AreEqual(0, changeCount, "Expected no changes");
  }


}//class
