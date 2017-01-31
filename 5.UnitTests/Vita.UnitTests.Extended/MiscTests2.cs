using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

using Vita.Common;
using Vita.Entities;
using Vita.Data.Driver;
using Vita.UnitTests.Common;
using Vita.Samples.BookStore;

namespace Vita.UnitTests.Extended {

  // Not real tests, simple demos. disabled for now
  public partial class MiscTests {

    [TestMethod]
    public void TestBugFixes() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false);
      var utcNow = app.TimeService.UtcNow; 

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
      if(Startup.ServerType != DbServerType.SqlCe) { //SQL CE does not allow subqueries
        var qEditorsOfProgBooks = session.EntitySet<IUser>().Where(u => u.BooksEdited.Any(b => b.Category == BookCategory.Programming));
        var editorsOfProgBooks = qEditorsOfProgBooks.ToList();
      }

      // Count + LIMIT is not supported by any server; check that appropriate error message is thrown
      session = app.OpenSession();
      session.EnableCache(false);
      var query = session.EntitySet<IBook>().Skip(1).Take(1);
      int count;
      var exc = TestUtil.ExpectFailWith<Exception>(() => count = query.Count());
      Assert.IsTrue(exc.Message.Contains("does not support COUNT"));

      //Fix for bug with MS SQL - operations like ">" in SELECT list are not supported
      // Another trouble: MySql stores bools as UInt64, but comparison results in Int64
      session = app.OpenSession();
      var hasFiction = session.EntitySet<IBook>().Any(b => b.Category == BookCategory.Fiction);
      Assert.IsTrue(hasFiction, "Expected hasFiction to be true");

      //Test All(); SQL CE does not support subqueries
      if(Startup.ServerType != DbServerType.SqlCe) {
        session = app.OpenSession();
        session.EnableCache(false); //don't mess with cache here
        var books = session.EntitySet<IBook>().Where(b => b.Authors.All(a => a.LastName != null)).ToArray();
        Assert.IsTrue(books.Length > 0, "Expected all books");
      }

      //Test for bug fix - loading DateTime? property from SQLite db
      var allBooks = session.GetEntities<IBook>();
      var bkNotPublished = allBooks.FirstOrDefault(bk => bk.PublishedOn == null);
      Assert.IsNotNull(bkNotPublished, "Failed to find not published book.");

      // Test that using entity-type parameter with null value works
      if(Startup.ServerType != DbServerType.SqlCe) {
        // SQL CE does not accept expressions like "@P1 IS NULL"
        IUser nullUser = null;
        var authorsNonUsers = session.EntitySet<IAuthor>().Where(a => a.User == nullUser).ToList();
        Assert.IsTrue(authorsNonUsers.Count > 0, "Failed to find non users");
      }

      //Query by null in nullable column - find not published book; there must be IronMan comic there.
      var qBooksNotPublished = from b in allBooks
                               where b.PublishedOn == null
                               select b;
      var lstBooksNotPublished = qBooksNotPublished.ToList();
      Assert.IsTrue(lstBooksNotPublished.Count > 0, "Failed to find not published book.");

      //Testing that SQLite DbCommand is disconnected - this releases server-side resources
      if(Startup.ServerType == DbServerType.Sqlite) {
        app.Flush(); //force flushing all logs, so no more background saves
        session = app.OpenSession();
        var fileName = "VitaBooksSQLite.db";
        var xfileName = "X_VitaBooksSQLite.db";
        Assert.IsTrue(System.IO.File.Exists(fileName));
        if(File.Exists(xfileName))
          File.Delete(xfileName); //in case file is left from previous failure
        File.Move(fileName, xfileName); //using Move to rename the file
        File.Move(xfileName, fileName); //rename back
        var newUser = session.NewUser("testUser", UserType.Customer);
        session.SaveChanges();
        File.Move(fileName, xfileName); //rename
        File.Move(xfileName, fileName); //rename back
      }

      //Test new/delete entity - this was a bug
      session = app.OpenSession();
      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var newOrd = session.NewOrder(dora);
      session.DeleteEntity(newOrd);
      session.SaveChanges();
    }

    [TestMethod]
    public void TestDbViews() {
      //SQL CE does not support views
      if(Startup.ServerType == DbServerType.SqlCe)
        return;
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false);
      if(Startup.ServerType == DbServerType.Postgres) {
        //Postgres requires to manually refresh materialized views
        session.ExecuteNonQuery(@"Refresh materialized view ""books"".""vBookSales"";");
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
      var upgradeMgr = new Vita.Data.Upgrades.DbUpgradeManager(ds);
      var upgradeInfo = upgradeMgr.BuildUpgradeInfo(); //.AddDbModelChanges(currentDbModel, modelInDb, DbUpgradeOptions.Default, app.ActivationLog);
      Assert.AreEqual(0, upgradeInfo.TableChanges.Count + upgradeInfo.NonTableChanges.Count, "Expected no changes");
    }

  }//class
}
