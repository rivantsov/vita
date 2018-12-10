using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

using Vita.Entities;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Modules.Login;
using Vita.Samples.BookStore;

namespace Vita.Testing.ExtendedTests {

  [TestClass]
  public class LinqNonQueryTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown(); 
    }

    [TestMethod]
    public void TestLinqNonQuery_Update () {
      var app = Startup.BooksApp;
      IEntitySession session;
      int updateCount;

      // LINQ update query should return auto object containting PK (Id) value and Properties to update (matched by name).
      // If Update does not involve other tables, the Id might be skipped in the output of the query - it will be ignored anyway

      #region Simple update involving 1 table (the table being updated) ----------------------------------
      session = app.OpenSession();
      session.EnableCache(false);
      //Pre-load old values, to compare them with new updated values
      var fictionBook0 = session.EntitySet<IBook>().First(b => b.Category == BookCategory.Fiction);
      var nonFictionBook0 = session.EntitySet<IBook>().First(b => b.Category != BookCategory.Fiction);
      // query returns new value for Price property, without book ID
      var booksQuery = from b in session.EntitySet<IBook>()
                       where b.Category == BookCategory.Fiction
                       select new { Price = b.Price + 1 }; 
      //update 
      updateCount = session.ExecuteUpdate<IBook>(booksQuery);
      //verify with new session
      session = app.OpenSession();
      session.EnableCache(false);
      var updatedFictionBook0 = session.GetEntity<IBook>(fictionBook0.Id);
      var updatedNonFictionBook0 = session.GetEntity<IBook>(nonFictionBook0.Id);
      Assert.AreEqual(fictionBook0.Price + 1, updatedFictionBook0.Price, "Expected increased price for fiction book.");
      Assert.AreEqual(nonFictionBook0.Price, updatedNonFictionBook0.Price, "Expected same price for non-fiction book.");
      #endregion

      #region Simple update involving setting Null value --------------------------------------------------------------------
      // Let's update Description column in books with Description = null; then revert these back to NULL
      var noDescrCount = session.EntitySet<IBook>().Where(b => b.Description == null).Count();
      Assert.IsTrue(noDescrCount > 0, "Expected some books with Description = null");
      var defaultDescr = "(No Description)";
      var updateDescrQuery = from b in session.EntitySet<IBook>()
                                where b.Description == null
                                select new { Description = defaultDescr };
      var updateDescrCount = session.ExecuteUpdate<IBook>(updateDescrQuery);
      Assert.AreEqual(noDescrCount, updateDescrCount, "Update count does not match.");
      // count again books with Descr = null; should be zero
      session = app.OpenSession();
      session.EnableCache(false); 
      var noDescrCoun0 = session.EntitySet<IBook>().Where(b => b.Description == null).Count();
      Assert.AreEqual(0, noDescrCoun0, "Expected no books with no description.");
      // Update back to null
      var revertDescrQuery = from b in session.EntitySet<IBook>()
                                where b.Description == defaultDescr
                                select new { Id = b.Id, Description = (string)null }; // explicit type required here
      session.ExecuteUpdate<IBook>(revertDescrQuery);
      //verify that noDescr count is back to original
      session = app.OpenSession();
      session.EnableCache(false);
      Thread.Sleep(50); // to let cache refresh, occasionally failing when cache is enabled
      var noDescrCount1 = session.EntitySet<IBook>().Where(b => b.Description == null).Count();
      Assert.AreEqual(noDescrCount, noDescrCount1,
        "Expected no-descr count back to original. Note: ignore, will be fixed when entity cache is refactored.");
      #endregion

      #region Linq update statement using data from several tables.
      // This results in a bit more complex SQL with FROM clause
      // Only MS SQL and Postgres support this syntax; For MySql there's a workaround (using JOIN), but this is not implemented yet
      if(Startup.ServerType == DbServerType.MsSql || Startup.ServerType == DbServerType.Postgres) {
        // Let's update order totals from SUM of order lines
        // First let's reset all totals to zero
        var setZerosQuery = from bo in session.EntitySet<IBookOrder>()
                            select new { Id = bo.Id, Total = 0 };
        var count = session.ExecuteUpdate<IBookOrder>(setZerosQuery);
        session = app.OpenSession();
        var countNonZeroTotal = session.EntitySet<IBookOrder>().Where(o => o.Total > 0).Count();
        Assert.AreEqual(0, countNonZeroTotal, "Expected no nonzero totals.");

        var ordersTotalsQuery = from bol in session.EntitySet<IBookOrderLine>()
                                group bol by bol.Order.Id into orderUpdate
                                select new { Id = orderUpdate.Key, Total = orderUpdate.Sum(line => line.Price * line.Quantity) };
        var totalsCount = ordersTotalsQuery.Count();
        Assert.IsTrue(totalsCount > 0, "Expected totals.");
        //Update
        var updatedTotalsCount = session.ExecuteUpdate<IBookOrder>(ordersTotalsQuery);
        Assert.AreEqual(totalsCount, updatedTotalsCount, "Totals update count mismatch.");
        var cmd = session.GetLastCommand();
        var sql = cmd.CommandText;
        //Let's check non-zero totals
        session = app.OpenSession();
        session.EnableCache(false);
        var countZeroTotals = session.EntitySet<IBookOrder>().Where(o => o.Lines.Count > 0 && o.Total == 0).Count(); //there is one order with zero lines
        Assert.AreEqual(0, countZeroTotals, "Expected no zero totals.");
        /*  SQL:
          UPDATE "books"."BookOrder"
           SET "Total" = _from."Total"
          FROM (
          SELECT "Order_Id" AS "Id_", SUM(("Price" * CONVERT(numeric,"Quantity"))) AS "Total"
          FROM "books"."BookOrderLine"
          GROUP BY "Order_Id"
               ) AS _from
          WHERE "Id" = _from."Id_";
        */

        // Special case - update query against 1 table with OrderBy and Skip/take. 
        // For ex, 2 queries to do the following: for the cheapest book in order give 10% discount; give 5% discound to other books
        // Because the query involves order-by and skip/take, the engine should use update SQL with FROM clause, not simplified update statement
        // (bug fix)
        session = app.OpenSession();
        session.EnableCache(false);
        //There is Diego's order with 3 books
        var diegoOrder = session.EntitySet<IBookOrder>().First(ord => ord.User.UserName == "Diego");
        Assert.AreEqual(3, diegoOrder.Lines.Count, "Expected 3 books in order.");
        var vbBkLine = diegoOrder.Lines[0]; //$25
        var csBkLine = diegoOrder.Lines[1]; //$20
        var winBkLine = diegoOrder.Lines[2];  //$30
                                              // query to update the most expensive item - add 1 cent to price
        var qUpdMostExp = session.EntitySet<IBookOrderLine>().Where(bol => bol.Order == diegoOrder)
                                                       .OrderByDescending(bol => bol.Price)
                                                       .Take(1)
                                                       .Select(bol => new { Id = bol.Id, Price = bol.Price + 0.01m });
        session.ExecuteUpdate<IBookOrderLine>(qUpdMostExp);
        //var cmd0 = session.GetLastCommand();
        // query to update the rest - reduce by 1 cent
        var qUpdNotMostExp = session.EntitySet<IBookOrderLine>().Where(bol => bol.Order == diegoOrder)
                                                       .OrderByDescending(bol => bol.Price)
                                                       .Skip(1)
                                                       .Select(bol => new { Id = bol.Id, Price = bol.Price - 0.01m });
        session.ExecuteUpdate<IBookOrderLine>(qUpdNotMostExp);
        // var cmd0 = session.GetLastCommand();
        //Verify prices changed
        EntityHelper.RefreshEntity(vbBkLine);
        EntityHelper.RefreshEntity(csBkLine);
        EntityHelper.RefreshEntity(winBkLine);
        Assert.AreEqual(24.99, (double)vbBkLine.Price, 0.001, "Expected price 24.99 for vbBook");
        Assert.AreEqual(19.99, (double)csBkLine.Price, 0.001, "Expected price 19.99 for csBook");
        Assert.AreEqual(30.01, (double)winBkLine.Price, 0.001, "Expected price 30.01 for winbook");
        //end special case


      } // if ServerType = MsSql or Postgres
      #endregion


      #region Updating entity references
      session = app.OpenSession();

      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var ferb = session.EntitySet<IUser>().First(u => u.UserName == "Ferb");

      //Let's reassign all Dora's reviews to Ferb. 
      // This goes against authorization rules, but authorization is not checked with LINQ update statements
      // Using FK column name directly (User_id) - I know, this is ugly, will be fixed in the future
      {
        var doraReviewCount0 = session.EntitySet<IBookReview>().Where(r => r.User == dora).Count();
        var updateQuery = from r in session.EntitySet<IBookReview>()
                          where r.User == dora
                          select new { Id = r.Id, User_Id = ferb.Id };
        //Execute update
        updateCount = session.ExecuteUpdate<IBookReview>(updateQuery);
        var doraReviewCount1 = session.EntitySet<IBookReview>().Where(r => r.User == dora).Count();
        Assert.AreEqual(0, doraReviewCount1, "Expected 0 review count for Dora.");
        var revertBackQuery = from r in session.EntitySet<IBookReview>()
                              where r.User == ferb
                              select new { Id = r.Id, User_Id = dora.Id };
        var updateBackCount = session.ExecuteUpdate<IBookReview>(revertBackQuery);
        var doraReviewCount2 = session.EntitySet<IBookReview>().Where(r => r.User == dora).Count();
        Assert.AreEqual(doraReviewCount0, doraReviewCount2, "Expected review count for Dora back to original.");
      }

      // Another way - using direct references to entities, this does not work
      /*
      {
        var doraReviewCount = session.EntitySet<IBookReview>().Where(r => r.User == dora).Count();
        Assert.IsTrue(doraReviewCount > 0, "Expected Dora's reviews.");
        var updateQuery = from r in session.EntitySet<IBookReview>()
                          where r.User == dora
                          select new { User = ferb };
        //Execute update
        updateCount = session.ExecuteUpdate<IBookReview>(updateQuery);
        Assert.AreEqual(doraReviewCount, updateCount, "Review update count does not match.");
        session = app.OpenSession();
        var doraReviewCount0 = session.EntitySet<IBookReview>().Where(r => r.User == dora).Count();
        Assert.AreEqual(0, doraReviewCount0, "Expected 0 review count for Dora.");
        // Assign back to Dora
        var revertBackQuery = from r in session.EntitySet<IBookReview>()
                              where r.User == ferb
                              select new { Id = r.Id, User = dora };
        var updateBackCount = session.ExecuteUpdate<IBookReview>(revertBackQuery);
        session = app.OpenSession();
        var doraReviewCount2 = session.EntitySet<IBookReview>().Where(r => r.User == dora).Count();
        Assert.AreEqual(doraReviewCount, doraReviewCount2, "Expected review count for Dora back to original.");
      }
      */
      #endregion
    }

    [TestMethod]
    public void TestLinqNonQuery_Delete() {
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var count0 = session.EntitySet<ICoupon>().Count(); 
      // Simple delete involving a single table
      //Create 3 coupons
      session.NewCoupon("Z-1", 11, DateTime.Now.AddMonths(1));
      session.NewCoupon("Z-2", 12, DateTime.Now.AddMonths(1));
      session.NewCoupon("Z-3", 13, DateTime.Now.AddMonths(1));
      session.SaveChanges(); 
      // new session, count coupons
      session = app.OpenSession();
      var count1 = session.EntitySet<ICoupon>().Count();
      Assert.AreEqual(count0 + 3, count1, "Expected 3 more coupons");
      // delete these Z- coupons using LINQ query
      var delQuery = from c in session.EntitySet<ICoupon>()
                     where c.PromoCode.StartsWith("Z-")
                     select c;
      var countDel = session.ExecuteDelete<ICoupon>(delQuery);
      Assert.AreEqual(3, countDel, "Expected delete count = 3");
      /* SQL: 
        DELETE FROM "books"."Coupon"
          WHERE ("PromoCode" LIKE 'Z-%' ESCAPE '\');
       */
      // new session, count coupons
      session = app.OpenSession();
      var count2 = session.EntitySet<ICoupon>().Count();
      Assert.AreEqual(count0, count2, "Expected coupon count back to original number.");

      // More complex case, involving multiple tables. We delete reviews posted by some user identified by name prefix. 
      //  Base query uses join of IBookReview and IUser tables
      // MySql does not allow this kind of query (https://dev.mysql.com/doc/refman/5.0/en/delete.html) : 
      //     "Currently, you cannot delete from a table and select from the same table in a subquery."
      if(Startup.ServerType == DbServerType.MySql)
        return;
      // Create 3 test reviews
      var reviewCount0 = session.EntitySet<IBookReview>().Count(); 
      var ferb = session.EntitySet<IUser>().Where(u => u.UserName == "Ferb").Single(); 
      var csBook = session.EntitySet<IBook>().Where(b => b.Title.StartsWith("c#")).Single();
      session.NewReview(ferb, csBook, 1, "Review 1", "Text 1");
      session.NewReview(ferb, csBook, 2, "Review 2", "Text 2");
      session.NewReview(ferb, csBook, 3, "Review 3", "Text 3");
      session.SaveChanges();
      var reviewCount1 = session.EntitySet<IBookReview>().Count();
      Assert.AreEqual(reviewCount0 + 3, reviewCount1, "Expected 3 new reviews");
      // Delete Ferb's reviews using LINQ-delete 
      session = app.OpenSession();
      // The base query must return IDs of reviews to delete
      var reviewDelQuery = from r in session.EntitySet<IBookReview>()
                           where r.User.UserName.StartsWith("Fe")
                           select r.Id; 
      countDel = session.ExecuteDelete<IBookReview>(reviewDelQuery);
      /* SQL: 
            DELETE FROM "books"."BookReview"   WHERE "Id" IN (
            SELECT r$."Id"
            FROM "books"."BookReview" r$, "books"."User" t1$
            WHERE (t1$."Id" = r$."User_Id") AND (t1$."UserName" LIKE 'Fe%' ESCAPE '\'));          
       */
      Assert.AreEqual(3, countDel, "Expected delete count = 3");
      session = app.OpenSession();
      var reviewCount2 = session.EntitySet<IBookReview>().Count();
      Assert.AreEqual(reviewCount0, reviewCount2, "Expected review count back to original number.");
    }

    [TestMethod]
    public void TestLinqNonQuery_Insert() {
      // Postgres: If this test fails, you need to run the following command and install uuid-ossp extension:
      //   CREATE EXTENSION "uuid-ossp";
      // SQLite: SQLite does not have a function for generating GUID, so inserts that require new GUIDs for PK columns do not work for SQLite
      if(Startup.ServerType == DbServerType.SQLite)
        return; 
      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      var dtNow = DateTime.UtcNow;
      var reviewCaption = "(Inserted Review)";
      var initialCount = session.EntitySet<IBookReview>().Count(); 
      // Method 1: using FK names directly (ex: Book_id)
      {
        var insertReviewQuery = from b in session.EntitySet<IBook>()
                                where b.Category == BookCategory.Programming
                                select new {
                                  Id = Guid.NewGuid(), CreatedOn = dtNow, Book_Id = b.Id, User_Id = dora.Id,
                                  Rating = 1, Caption = reviewCaption, Review = "(Inserted Review)",
                                };
        var newReviewsCount = insertReviewQuery.Count();
        Assert.IsTrue(newReviewsCount > 0, "Expected >1 .");
        // Let's add reviews 
        var insertCount = session.ExecuteInsert<IBookReview>(insertReviewQuery);
        Assert.AreEqual(newReviewsCount, insertCount, "Insert count does not match.");
        // let's delete them
        var deleteQuery = session.EntitySet<IBookReview>().Where(r => r.Caption == reviewCaption);
        var deleteCount = session.ExecuteDelete<IBookReview>(deleteQuery);
        //Check that review count is back to original
        var finalCount = session.EntitySet<IBookReview>().Count();
        Assert.AreEqual(initialCount, finalCount, "Expected review count back to original.");
      }

    }//method

    [TestMethod]
    public void TestLinqNonQuery_ScheduledCommands() {
      const string NewCode = "(NewCode)"; 
    var app = Startup.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false);

      // Set descriptions and schedule update of special codes
      var noDescrBooks = session.EntitySet<IBook>().Where(b => b.Description == null).ToList();
      foreach (var bk in noDescrBooks)
        bk.Description = "No description" + new string('.', 100); //to force using parameter in batch
      var booksUpdateQuery = from bk in session.EntitySet<IBook>()
                        where bk.Category == BookCategory.Programming  && bk.SpecialCode == null
                        select new {Id = bk.Id, SpecialCode = NewCode};
      //Schedule the query, so it should executed with SaveChanges, at transaction end, right before commit 
      session.ScheduleUpdate<IBook>(booksUpdateQuery, CommandSchedule.TransactionEnd); //TransactionEnd is default
      session.SaveChanges();

      //Open fresh session and check; 
      session = app.OpenSession();
      var bksWithNewCode = session.EntitySet<IBook>().Where(b => b.SpecialCode == NewCode).ToList();
      Assert.IsTrue(bksWithNewCode.Count > 0, "Expected some books with new special code");
      //check and revert back
      foreach (var bk in bksWithNewCode) {
        Assert.AreEqual(NewCode, bk.SpecialCode, "Expected new code");
        bk.SpecialCode = null;
      }
      session.SaveChanges();
    }
  }//class

}
