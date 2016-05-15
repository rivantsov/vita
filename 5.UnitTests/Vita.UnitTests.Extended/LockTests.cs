using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.Api;
using Vita.Entities.Runtime;
using Vita.Data.Driver;

namespace Vita.UnitTests.Extended {

  // Not real tests, simple demos. disabled for now
  // [TestClass]
  public class LockTests {

    [TestInitialize]
    public void TestInit() {
      SetupHelper.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      SetupHelper.TearDown(); 
    }

    [TestMethod]
    public void TestLock() {
      // Test for record locking facilities
      var app = SetupHelper.BooksApp;
      var session = app.OpenSession();
      var doraOrder = session.EntitySet<IBookOrder>().First(bo => bo.User.UserName == "Dora");
      var orderId = doraOrder.Id; 

      session = app.OpenSession();
      session.EnableCache(false); 
      var entSesson = (EntitySession)session;
      //session.Context.DbConnectionMode = DbConnectionReuseMode.KeepOpen; 

      var lockedBook = session.GetEntity<IBookOrder>(orderId, LockOptions.ForUpdate); //this should start transaction
      var trans = entSesson.CurrentConnection.DbTransaction;
      Assert.IsNotNull(trans, "Lock transaction did not start.");
      // var lockedBook = session.EntitySet<IBookOrder>(LockOptions.Update).First(o => o.Id == orderId);
      var lines = session.EntitySet<IBookOrderLine>().Where(bol => bol.Order.Id == orderId);
      foreach (var line in lines)
        line.Price -= 0.01m;
      session.SaveChanges(); //it should commit transaction

      //do it again, test that trans is started - checking bug because of cached query
      var lockedBook2 = session.GetEntity<IBookOrder>(orderId, LockOptions.ForUpdate); //this should start transaction
      Assert.IsNotNull(entSesson.CurrentConnection.DbTransaction, "Lock transaction did not start.");
      session.ReleaseLocks();
      var currConn = entSesson.CurrentConnection; 
      Assert.IsTrue(currConn == null || currConn.DbTransaction == null, "Lock transaction did not commit after ReleaseLocks.");


      var log = session.Context.LocalLog.GetAllAsText();
      Debug.WriteLine("================== LOG ==============================");
      Debug.WriteLine(log);
    }

    [TestMethod]
    public void TestLockNoLock() {
      // Test for no-lock mode for SELECT/Linq queries ( With(NoLock) for MS SQL)
      var app = SetupHelper.BooksApp;
      var session = app.OpenSession();
      session.EnableCache(false);
      // We use query with join, to verify how SQL is formatted when there's table alias
      var q0 = from pol in session.EntitySet<IBookOrderLine>(LockOptions.NoLock)
               where pol.Book.Price > 10
               select pol.Book.Title;
      var lst0 = q0.ToList();
      Assert.IsTrue(lst0.Count > 0, "Failed to retrieve books with NoLock option.");
      //Let's check that no-lock was actually used.
      var cmd = session.GetLastCommand();
      if (SetupHelper.ServerType == DbServerType.MsSql) {
        //For MS SQL, we set individual With(NoLock) hints on all tables. 
        // Setting transaction isolation level like in MySql is very unsafe. The problem is that with connection pooling the sp_reset_connection stored proc 
        // that prepares connection after getting it from the pool does NOT reset the default isolation level. So we have to ensure that we flip isolation
        // level back to default, whatever it is. This gets more complicated considering that SQL might fail and throw error
        // - we then need to set a catch/finally block or something. So instead we are using NoLock hint on all tables. 
        var index = cmd.CommandText.IndexOf("WITH(NOLOCK)", StringComparison.InvariantCultureIgnoreCase);
        Assert.IsTrue(index > 0, "No-lock query does not contain WITH(NOLOCK) hint.");
      }
      if (SetupHelper.ServerType == DbServerType.MySql) {
        // For MySql, there are no individual NoLock hints on tables. We can only set READ UNCOMMITTED on 
        var index = cmd.CommandText.IndexOf("READ UNCOMMITTED", StringComparison.InvariantCultureIgnoreCase);
        Assert.IsTrue(index > 0, "No-lock query does not contain 'READ UNCOMMITTED' hint.");
      }
    }

    
    //Test sample for locking guide, not a real unit test
    [TestMethod]
    public void TestLockSamples() {
      var app = SetupHelper.BooksApp; 
      var session = app.OpenSystemSession();
      var order = session.EntitySet<IBookOrder>().First(bo => bo.User.UserName == "Dora");
      var oldLineCount = order.Lines.Count; 
      var bk = session.EntitySet<IBook>().First(b => b.Title.StartsWith("Iron"));
      //open fresh session
      session = app.OpenSystemSession();
      session.EnableCache(false);
      session.LogMessage("/* ***************** AddBookToOrder ********************* */");
      AddBookToOrder(session, order.Id, bk.Id, 1);
      //session = app.OpenSystemSession();
      session.LogMessage("/* ***************** GetBookStats   ********************* */");
      var orderModel = GetBookOrderStats(session, order.Id);
      Assert.AreEqual(oldLineCount + 1, orderModel.LineCount, "Line/item count does not match.");
      // check that connection is closed 
      var currConn = ((EntitySession)session).CurrentConnection;
      Assert.IsNull(currConn, "Expected connection disposed");
      var log = session.Context.LocalLog.GetAllAsText();
      Debug.WriteLine(log); 
    }

    public void AddBookToOrder(IEntitySession session, Guid orderId, Guid bookId, int quantity) {
      var order = session.GetEntity<IBookOrder>(orderId, LockOptions.ForUpdate);
      var book = session.GetEntity<IBook>(bookId);
      var orderLine = session.NewEntity<IBookOrderLine>();
      orderLine.Order = order;
      orderLine.Book = book;
      orderLine.Quantity = quantity;
      orderLine.Price = book.Price;
      order.Lines.Add(orderLine);
      order.Total = order.Lines.Sum(ol => ol.Price * ol.Quantity);
      session.SaveChanges(); 
    }

    public BookOrderStats GetBookOrderStats(IEntitySession session, Guid orderId) {
      try {
        var order = session.GetEntity<IBookOrder>(orderId, LockOptions.SharedRead);
        return new BookOrderStats() { 
          OrderId = orderId, LineCount = order.Lines.Count, 
          MaxPrice = order.Lines.Max(ol => ol.Price) };
      } finally {
        session.ReleaseLocks(); 
      }
    }

    public class BookOrderStats {
      public Guid OrderId;
      public int LineCount;
      public decimal MaxPrice; 
    }
 
    
  }//class
}
