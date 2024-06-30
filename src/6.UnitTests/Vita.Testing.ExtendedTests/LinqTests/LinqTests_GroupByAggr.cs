using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using BookStore;
using Vita.Tools.Testing;

namespace Vita.Testing.ExtendedTests {

  public partial class LinqTests {

    //There are 4 overloads of Queryable.GroupBy that we need to support (another 4 with IComparer as last parameter are not supported).
    // Test them all here.
    [TestMethod]
    public void TestLinqGroupBy() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();

      //overload #1; for this method grouping occurs in CLR
      var query1 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id);
      var list1 = query1.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list1.Count > 0, "overload #1 failed.");

      //overload #2; grouping in CLR
      // Select pairs of publisher id, book titles
      var query2 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id, b => b.Title); ;
      var list2 = query2.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list2.Count > 0, "overload #2 failed.");
      //slight variation of #2, with autotype and New
      var query2b = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id, b => new { Title = b.Title, Price = b.Price }); ;
      var list2b = query2b.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list2b.Count > 0, "overload #2-B failed.");

      //overload #3 - grouping in SQL
      // Select pairs of publisher id, book count, average price
      var query3 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id,
        (id, books) => new { Id = id, Count = books.Count(), AvgPrice = books.Average(b => b.Price) });
      var list3 = query3.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list3.Count > 0, "overload #3 failed.");


      // SQL-like syntax Group-By
      var booksByCat = from b in session.EntitySet<IBook>()
                       group b by b.Category into g
                       orderby g.Key
                       select new { Category = g.Key, BookCount = g.Count(), MaxPrice = g.Max(b => b.Price) };
      var lstBooksByCat = booksByCat.ToList();
      LogLastQuery(session);
      Assert.IsTrue(lstBooksByCat.Count > 0, "GroupBy test returned 0 groups.");

      //Some special queries
      // - groupBy followed by Select
      var queryS1 = session.EntitySet<IBook>().GroupBy(b => b.Publisher.Id).Select(g => new { Id = g.Key, Count = g.Count() });
      var listS1 = queryS1.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listS1.Count > 0, "Special test 1 failed.");

      // -- select followed by GroupBy
      var queryS2 = session.EntitySet<IBook>().Join(
          session.EntitySet<IPublisher>(), b => b.Publisher.Id, p => p.Id, (b, p) => new { PubId = p.Id, BookId = b.Id })
               .GroupBy(bp => bp.PubId, (pubId, pairs) => new { PubId = pubId, BookCount = pairs.Count() });
      var listS2 = queryS2.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listS2.Count > 0, "Special test 2 (select followed by GroupBy) failed.");

      // GroupBy nullable field - this is a special case; Null values will come out as type default in group key;
      // So for authors that are not users (author.User is null), the resulting group will have key = Guid.Empty
      // this fails with entity cache
      var queryS3 = session.EntitySet<IAuthor>().GroupBy(a => a.User.Id).Select(g => new { Id = g.Key, Count = g.Count() });
      var listS3 = queryS3.ToList();
      LogLastQuery(session);
      Assert.IsTrue(listS3.Count > 0, "Special test 3 (Group by nullable key) failed.");

      //Aggregates with fake group by - returning agregates without group by
      var dora = session.EntitySet<IUser>().First(u => u.UserName == "Dora");
      //get count, average of book order
      var doraOrderStats = from ord in session.EntitySet<IBookOrder>()
                           where ord.User == dora
                           group ord by 0 into g
                           select new { Count = g.Count(), Avg = g.Average(o => o.Total) };
      var stats = doraOrderStats.ToList();
      Assert.AreEqual(1, stats.Count, "Expected 1 stats record");
      var stat0 = stats[0];
      Assert.IsTrue(stat0.Count > 0 && stat0.Avg > 0, "Expected non-zero count and avg.");
      /* SQL: 
          SELECT COUNT_BIG(*) AS "Count", AVG("Total") AS "Avg"
          FROM "books"."BookOrder"
          WHERE ("User_Id" = @P0)
       */
    }

    [TestMethod]
    public void TestLinqAggregates() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();

      // Calc Avg in SQL and compare with Avs over entities in c#
      var avgBkPrice = session.EntitySet<IBook>().Average(b => b.Price);
      var avgBkPrice2 = session.GetEntities<IBook>().Average(b => b.Price);
      var avgDiff = Math.Abs(avgBkPrice - avgBkPrice2);
      Assert.IsTrue(avgDiff < 0.01m, "Avg mismatch - in db vs in c#");

      // Bug fix: Avg over Int field - fixed
      var avgOrderCnt = session.EntitySet<IBookOrderLine>().Average(ol => ol.Quantity);
      Assert.IsTrue(avgOrderCnt > 0, "Expected avg > 0");

      /*
       //currently does not work - join with non-table/subquery not supported
      var bkSet = session.EntitySet<IBook>();
      var bolGroups = session.EntitySet<IBookOrderLine>()
                                   .Where(bl => bl.Order.Status == OrderStatus.Completed)
                                   .GroupBy(bl => bl.Book.Id)
                                   .Select (g => new {
                                     BookId = g.Key, Copies = g.Sum(l => l.Quantity), Total = g.Sum(l => l.Quantity * l.Price)
                                   });
      var bkSalesQ = from bk in session.EntitySet<IBook>()
                     join bolG in bolGroups.DefaultIfEmpty() on bk.Id equals bolG.BookId
                     //into bolSets
                     select new { bk.Title, Count = bolG.Copies, Total = bolG.Total };
      var bkSales = bkSalesQ.ToList();
      foreach (var bk in bkSales) {
        Trace.WriteLine($"  Book '{bk.Title}', Copies sold: {bk.Count}, Total: {bk.Total}");
      }
      */
      // Testing the problem with materialized queries. This query is exactly the same 
      var bookSet = session.EntitySet<IBook>();
      var bolSet = session.EntitySet<IBookOrderLine>(); 
      var bookSalesQuery = from b in bookSet
                            select new {
                              Id = b.Id, Title = b.Title, Publisher = b.Publisher.Name,
                              Count = bolSet.Where(bol => bol.Book == b).Sum(bol => bol.Quantity),
                              Total = bolSet.Where(bol => bol.Book == b).Sum(bol => bol.Price * bol.Quantity)
                            };
      var bkSales = bookSalesQuery.ToList(); 
      var cmd = session.GetLastCommand();
      foreach (var bk in bkSales) {
        Trace.WriteLine($"  Book '{bk.Title}', Copies sold: {bk.Count}, Total: {bk.Total}");
      }


      var bookSalesQuery2 = from p in
                           (from bk in session.EntitySet<IBook>()
                           join bol in session.EntitySet<IBookOrderLine>().Where(bl => bl.Order.Status == OrderStatus.Completed) 
                                  on bk.Id equals bol.Book.Id
                                into bolSets
                           from bol in bolSets.DefaultIfEmpty()
                           select new { BK = bk, BOL = bol})
                           group p by new { p.BK.Id, p.BK.Title } into g
                           select new {
                             Title = g.Key.Title,
                             Count = g.Sum(bl => bl.BOL.Quantity),
                             Total = g.Sum(bl => bl.BOL.Quantity * bl.BOL.Price)
                           };
      var bkSales2 = bookSalesQuery2.ToList();
      cmd = session.GetLastCommand();
      Trace.WriteLine(cmd.CommandText);

      foreach (var bk in bkSales2) {
        Trace.WriteLine($"  Book '{bk.Title}', Copies sold: {bk.Count}, Total: {bk.Total}");
      }
    }

  }//class

}
