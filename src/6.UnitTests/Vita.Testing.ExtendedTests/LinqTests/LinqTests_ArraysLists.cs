using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using BookStore;
using Vita.Tools.Testing;

namespace Vita.Testing.ExtendedTests {

  public partial class LinqTests {

    [TestMethod]
    public void TestLinqArrayContains() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();

      var bookOrders = session.EntitySet<IBookOrder>();
      //Note: for debugging use table that is not fully cached, so we use IBookOrder entity

      // Test retrieving orders by Id-in-list
      var someOrders = bookOrders.Take(2).ToList();
      var someOrderIds = someOrders.Select(o => o.Id).ToArray();
      var qSomeOrders = from bo in bookOrders
                        where someOrderIds.Contains(bo.Id)
                        select bo;
      var someOrders2 = qSomeOrders.ToList();
      var cmd = session.GetLastCommand(); //just for debugging
      Assert.AreEqual(someOrderIds.Length, someOrders2.Count, "Test Array.Contains failed: order counts do not match.");

      // Try again with a single Id
      var arrOneId = new Guid[] { someOrderIds[0] };
      var qOrders = from bo in bookOrders
                    where arrOneId.Contains(bo.Id)
                    select bo;
      var orders = qOrders.ToList();
      Assert.AreEqual(1, orders.Count, "Test Array.Contains with one Id failed: order counts do not match.");

      // Again with empty list
      var arrEmpty = new Guid[] { };
      var qNoBooks = from b in session.EntitySet<IBook>()
                     where arrEmpty.Contains(b.Id)
                     select b;
      var noBooks = qNoBooks.ToList();
      cmd = session.GetLastCommand();
      Assert.AreEqual(0, noBooks.Count, "Test Array.Contains with empty array failed, expected 0 entities");

      // Empty list, no parameters option - should be 'literal empty list' there, depends on server type
      qNoBooks = from b in session.EntitySet<IBook>().WithOptions(QueryOptions.NoParameters)
                 where arrEmpty.Contains(b.Id)
                 select b;
      noBooks = qNoBooks.ToList();
      cmd = session.GetLastCommand();
      Assert.AreEqual(0, noBooks.Count, "Expected 0 entities, empty-list-contains with literal empty list");
      Assert.AreEqual(0, cmd.Parameters.Count, "Expected 0 db params with NoParameters option");

      // Again with list, not array
      var orderIdsList = someOrderIds.ToList();
      qOrders = from bo in bookOrders
                where orderIdsList.Contains(bo.Id)
                select bo;
      orders = qOrders.ToList();
      Assert.AreEqual(orderIdsList.Count, orders.Count,
          "Test constList.Contains, repeated query failed: order counts do not match.");

      // Again with NoParameters options - force using literals
      qOrders = from bo in bookOrders.WithOptions(QueryOptions.NoParameters)
                where orderIdsList.Contains(bo.Id)
                select bo;
      orders = qOrders.ToList();
      Assert.AreEqual(orderIdsList.Count, orders.Count,
          "Test constList.Contains, no-parameters linq query failed: order counts do not match.");
      cmd = session.GetLastCommand();
      Assert.AreEqual(0, cmd.Parameters.Count, "NoParameters option - expected no db parameters");


      // Test array.Contains()
      var userTypes = new UserType[] { UserType.Customer, UserType.Author };
      var qOrders2 = from bo in bookOrders
                     where userTypes.Contains(bo.User.Type)
                     select bo;
      var orders2 = qOrders2.ToList();
      Assert.IsTrue(orders2.Count > 0, "No orders by type found.");

      // Used to fails for postgres, May 2024 - fixed it 
      // Test List<enum>.Contains() - used to fails for Postgres, for enums array works but List fails
      // Fixed by converting list to array
      var userTypesList = new List<UserType>() { UserType.Customer, UserType.Author };
      qOrders2 = from bo in bookOrders
                     where userTypesList.Contains(bo.User.Type)
                     select bo;
      orders2 = qOrders2.ToList();
      Assert.IsTrue(orders2.Count > 0, "No orders by type found.");

    }

    [TestMethod]
    public void TestLinqArrayParameters() {
      Startup.BooksApp.LogTestStart();

      // Not all servers support array parameters
      var session = Startup.BooksApp.OpenSession();
      // count prog books reviews 
      var progReviewCount = session.EntitySet<IBookReview>().Where(r => r.Book.Category == BookCategory.Programming).Count();
      var progBooks = session.EntitySet<IBook>().Where(b => b.Category == BookCategory.Programming).ToList();

      //This array will be passed to db as parameter; MS SQL - converted to DataTable, Postgres - as an array
      var bookIds = progBooks.Select(b => b.Id).ToArray();
      var reviewQuery = session.EntitySet<IBookReview>().Where(r => bookIds.Contains(r.Book.Id));
      var reviews = reviewQuery.ToList();
      Assert.AreEqual(progReviewCount, reviews.Count, "Invalid review count");
      var cmd = session.GetLastCommand();
      //Debug.WriteLine(cmd.CommandText);

      //try list of strings (not array)
      var names = new List<string>(new string[] { "John", "Duffy", "Dora" });
      var selUsers = session.EntitySet<IUser>().Where(u => names.Contains(u.UserName)).ToList();
      Assert.AreEqual(names.Count, selUsers.Count, "Expected some users");
      cmd = session.GetLastCommand();
      //Debug.WriteLine(cmd.CommandText);
    }

    [TestMethod]
    public void TestLinqContains() {
      Startup.BooksApp.LogTestStart();

      var app = Startup.BooksApp;
      var session = app.OpenSession();
      var orderLines = session.EntitySet<IBookOrderLine>();

      // Find books that have NOT been ordered - try by IDs
      var q0 = from b in session.EntitySet<IBook>()
               where !orderLines.Select(bol => bol.Book.Id).Contains(b.Id)
               select b;
      var list0 = q0.ToList();
      LogLastQuery(session);
      Assert.IsTrue(list0.Count > 0, "Query for not ordered books (Ids) failed.");

      // The same, only using directly book reference
      // entitySet.Contains(ent) - this also works
      var qBooksNotPurchased = from b in session.EntitySet<IBook>()
                               where !orderLines.Select(bol => bol.Book).Contains(b)
                               select b;
      var lstBooksNotPurchased = qBooksNotPurchased.ToList();
      LogLastQuery(session);
      Assert.IsTrue(lstBooksNotPurchased.Count > 0, "Query with Contains(book) failed.");

      //One very special case - pub.Books.Contains(...) method. In this case, Contains is not Queryable.Contains, but ICollection.Contains(item)
      // - because pub.Books is IList<IBook>, so compiler picks Contains instance method on the class/interface, before extension method. 
      // SQL translator makes special treatment of this case
      var csBook = session.EntitySet<IBook>().First(b => b.Title == "c# Programming");
      var qCsBkPub = from pub in session.EntitySet<IPublisher>()
                     where pub.Books.Contains(csBook)
                     select pub;
      var csBookPubs = qCsBkPub.ToList();
      LogLastQuery(session);
      Assert.IsTrue(csBookPubs.Count == 1, "Query publisher by book failed. ");

    } //method



  }//class

}
