using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

using Vita.Entities;
using BookStore;
using BookStore.Api;
using Vita.Tools.Testing;
using Arrest.Sync;

namespace Vita.Testing.WebTests {

  public partial class BooksApiTests  {

    [TestMethod]
    public void TestUserReviews() {
      var client = Startup.Client;
      var csBk = client.Get<SearchResults<Book>>("api/books?title={0}", "c#").Results[0];
      //get details - with reviews
      var csBkDet = client.Get<Book>("api/books/{0}", csBk.Id);
      Assert.IsNotNull(csBkDet, "Failed to get c# book details.");
      //check reviews are returned
      Assert.IsTrue(csBkDet.LatestReviews.Count > 0, "Reviews not returned");
      //get review by id
      var reviewId = csBkDet.LatestReviews[0].Id;
      var review = client.Get<BookReview>("api/reviews/{0}", reviewId);
      Assert.IsNotNull(review, "Failed to get review");

      //Let's login as Diego and post a review for cs book
      var diegoReview = new BookReview() { BookId = csBk.Id, Rating = 2, Caption = "Not impressed", Review = "Outdated, boring" };
      LoginAs("Diego");
      diegoReview = client.Post<BookReview, BookReview>(diegoReview, "api/user/reviews");
      Assert.IsNotNull(diegoReview, "Failed to post review");
      Assert.IsTrue(diegoReview.Id != Guid.Empty, "Expected non-empty review ID.");
      var diegoReviewId = diegoReview.Id;
      //Diego changes rating
      diegoReview.Rating = 1;
      client.Put<BookReview, BookReview>(diegoReview, "api/user/reviews");
      // let's read it back and check rating
      diegoReview = client.Get<BookReview>("api/reviews/{0}", diegoReview.Id);
      Assert.AreEqual(1, diegoReview.Rating, "Expected rating 1.");
      
      //Delete it
      var status = client.Delete("api/user/reviews/{0}", diegoReviewId);
      Assert.AreEqual(HttpStatusCode.OK, status, "Failed to delete.");
      // Try to read it back - should be null
      diegoReview = client.Get<BookReview>("api/reviews/{0}", diegoReviewId);
      Assert.IsNull(diegoReview, "Failed to delete review.");
      //Try to delete it again - should get ObjectNotFound
      var cfExc = TestUtil.ExpectClientFault(() => client.Delete("api/user/reviews/{0}", diegoReviewId));
      Assert.AreEqual(ClientFaultCodes.ObjectNotFound, cfExc.Faults[0].Code, "Expected object not found fault");

      Logout();
    } // method

    [TestMethod]
    public void TestUserOrders() {
      var client = Startup.Client;

      //Get dora's order(s) - she has 1 order for c# book; 
      LoginAs("dora");

      // dora's userId will be forced by controller into query condition
      var doraOrders = client.Get<SearchResults<BookOrder>>("api/user/orders?booktitle={0}&publisher=ms&mintotal=5", "c#");
      Assert.AreEqual(1, doraOrders.Results.Count, "Expected c# book order for dora.");
      var order0 = doraOrders.Results[0];
      Assert.IsNull(order0.Items, "Expected no items"); //items are returned when we get single order by id
      //get order details
      var orderDet = client.Get<BookOrder>("api/user/orders/{0}", order0.Id);
      Assert.IsTrue(orderDet.Items.Count > 0, "Expected items in Dora's book order."); //there must be 2 items
      // check tracking number - it is stored encrypted in db
      Assert.AreEqual("123456", orderDet.TrackingNumber, "Invalid tracking number");

      //cart
      var cart = client.Get<BookOrder>("api/user/cart");
      Assert.IsNull(cart, "Expected empty cart.");
      // Dora decides to buy iron man book
      var ironManSearch = client.Get<SearchResults<Book>>("api/books?title=iron");
      Assert.IsTrue(ironManSearch.Results.Count == 1, "Iron man book not found.");
      var ironManBook = ironManSearch.Results[0];
      // Create cart by simply adding a book to new order
      var ironManBookSlim = new Book() { Id = ironManBook.Id }; //we could use ironManBook, but we actually need only book Id there
      var ironManItem = new BookOrderItem() {Book = ironManBookSlim, Quantity = 1};
      ironManItem = client.Post<BookOrderItem, BookOrderItem>(ironManItem, "api/user/cart/item");
      Assert.IsNotNull(ironManItem, "Expected order item");
      // cart now is not empty and holds one item; cart total should be updated automatically
      cart = client.Get<BookOrder>("api/user/cart");
      Assert.IsNotNull(cart, "Expected not empty cart");
      Assert.AreEqual(1, cart.Items.Count, "Expected one item in the cart");
      Assert.IsTrue(Math.Abs(ironManBook.Price - cart.Total) < 0.1m, "Cart total does not match book price");
      // Let's change quantity (2 Iron Man books) and add "Windows Programming" book, and update order
      cart.Items[0].Quantity = 2;
      var winBook = client.Get<SearchResults<Book>>("api/books?title=windows").Results[0];
      var winBookItem = new BookOrderItem() { Book = winBook, Quantity = 3 };
      cart.Items.Add(winBookItem);
      //Update whole order
      var updatedCart = client.Put<BookOrder, BookOrder>(cart, "api/user/cart");
      var expectedTotal = ironManBook.Price * 2 + winBook.Price * 3;
      Assert.IsTrue(Math.Abs(expectedTotal - updatedCart.Total) < 0.1m, "Cart total does not match books price");
      //Let's remove windows book by setting Quantity = 0
      winBookItem.Quantity = 0;
      updatedCart = client.Put<BookOrder, BookOrder>(cart, "api/user/cart");
      Assert.IsTrue(updatedCart.Items.Count == 1, "Expected only one book in cart.");

      /* // Cancel order - tested once, now commented, submitting instead 
      client.ExecuteDelete("api/user/cart");
      cart = client.Get<BookOrder>("api/user/cart");
      Assert.IsNull(cart, "Expected empty cart.");
       */ 
      //Sumbit order
      var order = client.Put<object, BookOrder>(null, "api/user/cart/submit"); //no promo/coupon
      Assert.IsNotNull(order, "Expected submitted order");
      Assert.AreEqual(OrderStatus.Completed, order.Status, "Expected completed order");
      Assert.IsTrue(Math.Abs(order.Total - ironManBook.Price * 2) < 0.1m, "Total does not match.");
      //cart should be empty now
      cart = client.Get<BookOrder>("api/user/cart");
      Assert.IsNull(cart, "Expected empty cart.");

      Logout(); 
    }
  }//class
}
