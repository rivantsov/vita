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

  // Not real tests, sample code for locking guie
  // [TestClass]
  public class LockTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }
    [TestCleanup]
    public void TearDown() {
      Startup.TearDown(); 
    }
    //Test sample for locking guide, not a real unit test
    [TestMethod]
    public void TestLockSamples() {
      var app = Startup.BooksApp; 
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
