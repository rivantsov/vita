using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Api;
using Microsoft.AspNetCore.Mvc;
using Vita.Web;
using Microsoft.AspNetCore.Authorization;

namespace Vita.Samples.BookStore.Api {

  // Secured, authenticated-only controller for managing user-owned data
  [Route("api/user"), Authorize]
  public class UserAccountController : BaseApiController {

    public UserAccountController() {

    }

    #region User
    [HttpGet]
    public User GetCurrentUser() {
      var session = OpenSession();
      var user = session.GetEntity<IUser>(OpContext.User.UserId);
      return user.ToModel(); 
    }
    #endregion

    #region Cart, Orders
    [HttpGet, Route("orders")]
    public SearchResults<BookOrder> GetUserOrders([FromQuery] BookOrderSearch search) {
      search = search.DefaultIfNull(defaultOrderBy: "createdon-desc");
      var session = OpenSession();
      var where = session.NewPredicate<IBookOrder>()
        .And(bo => bo.Status != OrderStatus.Open) //Open order is shopping cart
        .AndIfNotEmpty(search.CreatedAfter, bo => bo.CreatedOn >= search.CreatedAfter.Value)
        .AndIfNotEmpty(search.CreatedBefore, bo => bo.CreatedOn <= search.CreatedBefore.Value)
        .AndIfNotEmpty(search.MinTotal, bo => bo.Total > search.MinTotal.Value);
      // book subquery
      var baseBookCond = session.NewPredicate<IBookOrderLine>();
      var bookCond = baseBookCond
        .AndIfNotEmpty(search.BookId, ol => ol.Book.Id == search.BookId.Value)
        .AndIfNotEmpty(search.BookTitle, ol => ol.Book.Title.StartsWith(search.BookTitle))
        .AndIfNotEmpty(search.BookCategory, ol => ol.Book.Category == search.BookCategory.Value)
        .AndIfNotEmpty(search.Publisher, ol => ol.Book.Publisher.Name.StartsWith(search.Publisher));
      if (bookCond != baseBookCond) { //if we added any conditions on book
        var subQuery = session.EntitySet<IBookOrderLine>().Where(bookCond).Select(bol => bol.Order.Id);
        where = where.And(bo => subQuery.Contains(bo.Id));
      }
      //if current user is a customer, force UserId in the query - user can only see his own orders
      var currUserId = OpContext.User.UserId;
      var user = session.GetEntity<IUser>(currUserId);
      if(user.Type == UserType.Customer || user.Type == UserType.Author)
          where = where.And(bo => bo.User.Id == currUserId);
      //run search
      var results = session.ExecuteSearch(where, search, o => o.ToModel(details: false), include: o => o.User);
      return results; 
    }
    /*  Sample SQL executed by this method, slightly formatted for readability (MS SQL): 
    SELECT t0$."Id", t0$."CreatedOn", t0$."Total", t0$."Status", t0$."CreatedIn", t0$."UpdatedIn", t0$."User_Id"
    FROM "books"."BookOrder" t0$
    WHERE ((1 = 1) AND (t0$."Status" <> 0) AND (t0$."Total" > @P0) AND (t0$."Id" IN 
     (SELECT t3$."Order_Id"
        FROM "books"."BookOrderLine" t3$
              INNER JOIN "books"."Book" t1$ ON t1$."Id" = t3$."Book_Id"
              INNER JOIN "books"."Publisher" t2$ ON t2$."Id" = t1$."Publisher_Id"
        WHERE (((1 = 1) AND t1$."Title" LIKE @P4 ESCAPE '\') AND t2$."Name" LIKE @P5 ESCAPE '\'))
     ) 
    AND (t0$."User_Id" = @P1))
    ORDER BY t0$."CreatedOn" DESC 
    OFFSET @P2 ROWS FETCH NEXT @P3 ROWS ONLY
     */

    [HttpGet, Route("orders/{id}")]
    public BookOrder GetOrderDetails(Guid id) {
      var session = OpenSession();
      try {
        var order = session.GetEntity<IBookOrder>(id, LockType.SharedRead);
        return order.ToModel(details: true);
      } finally {
        session.ReleaseLocks(); 
      }
    }

    [HttpGet, Route("cart")]
    public BookOrder GetCart() {
      var session = OpenSession();
      try {
        var openOrder = GetOpenOrder(session, LockType.SharedRead);
        return openOrder.ToModel(details: true); //if order is null, returns null - empty cart
      } finally {
        session.ReleaseLocks(); 
      }
    }

    [HttpPut, Route("cart")]
    public BookOrder UpdateOrder(BookOrder order) {
      var session = OpenSession();
      var cart = GetOpenOrder(session, LockType.ForUpdate, create: true);
      foreach (var item in order.Items) {
        OpContext.ThrowIf(item.Quantity < 0, ClientFaultCodes.InvalidValue, "Quantity", "Quantity may not be negative.");
        OpContext.ThrowIf(item.Quantity > 10, ClientFaultCodes.InvalidValue, "Quantity", "Quantity may not be more than 10.");
        var orderLine = cart.Lines.FirstOrDefault(ln => ln.Book.Id == item.Book.Id);
        if (orderLine == null) {
          if (item.Quantity > 0) {
            var bk = session.GetEntity<IBook>(item.Book.Id);
            OpContext.ThrowIfNull(bk, ClientFaultCodes.ObjectNotFound, "Book", "Book not found.");
            cart.Add(bk, item.Quantity);
            continue;
          }
        } else //orderLine != null        
          if (item.Quantity == 0)
            session.DeleteEntity(orderLine);
          else
            orderLine.Quantity = item.Quantity; 
      }
      cart.ScheduleUpdateTotal();
      session.SaveChanges();
      EntityHelper.RefreshEntity(cart); //to make sure total from database is refreshed in entity
      return cart.ToModel(details: true); 
    }


    [HttpPost, Route("cart/item")]
    public BookOrderItem AddOrderItem(BookOrderItem item) {
      var session = OpenSession();
      var bk = session.GetEntity<IBook>(item.Book.Id);
      OpContext.ThrowIfNull(bk, ClientFaultCodes.ObjectNotFound, "BookId", "Book not found.");
      var cart = GetOpenOrder(session, LockType.ForUpdate, create: true);
      var itemEnt = cart.Add(bk, item.Quantity);
      cart.ScheduleUpdateTotal();
      session.SaveChanges();
      return itemEnt.ToModel();
    }

    [HttpPut, Route("cart/item")]
    public BookOrderItem UpdateOrderItem(BookOrderItem item) {
      var session = OpenSession();
      //Lock order
      var order = session.GetEntity<IBookOrder>(item.OrderId, LockType.ForUpdate);
      var itemEnt = session.GetEntity<IBookOrderLine>(item.Id);
      OpContext.ThrowIfNull(itemEnt, ClientFaultCodes.ObjectNotFound, "BookId", "Book not found.");
      if (item.Quantity == 0)
        session.DeleteEntity(itemEnt);
      else 
        itemEnt.Quantity = item.Quantity;
      itemEnt.Order.ScheduleUpdateTotal();
      session.SaveChanges();
      return itemEnt.ToModel();
    }

    [HttpDelete, Route("cart/item/{id}")]
    public void DeleteOrderItem(Guid id) {
      var session = OpenSession();
      var itemEnt = session.GetEntity<IBookOrderLine>(id);
      OpContext.ThrowIfNull(itemEnt, ClientFaultCodes.ObjectNotFound, "BookId", "Book not found.");
      //Lock order
      var cart = GetOpenOrder(session, LockType.ForUpdate);
      session.DeleteEntity(itemEnt);
      itemEnt.Order.ScheduleUpdateTotal(); 
      session.SaveChanges();
    }

    [HttpPut, Route("cart/submit")]
    public BookOrder SubmitOrder(string coupon = null) {
      var session = OpenSession();
      var cart = GetOpenOrder(session, LockType.ForUpdate);
      OpContext.ThrowIfNull(cart, ClientFaultCodes.InvalidAction, "Cart", "Cart is empty, cannot submit order.");
      cart.CompleteOrder(coupon);
      session.SaveChanges(); 
      return cart.ToModel(details: true);
    }

    [HttpDelete, Route("cart")]
    public BookOrder CancelOrder() {
      //we use this args object to allow optional coupon parameter
      var session = OpenSession();
      var cart = GetOpenOrder(session, LockType.ForUpdate);
      OpContext.ThrowIfNull(cart, ClientFaultCodes.InvalidAction, "Cart", "Cart is empty, cannot submit order.");
      cart.Status = OrderStatus.Canceled;
      session.SaveChanges();
      return cart.ToModel(details: true);
    }


    private IBookOrder GetOpenOrder(IEntitySession session, LockType lockType, bool create = false) {
      var currUserId = OpContext.User.UserId;
      var openOrder = session.EntitySet<IBookOrder>(lockType)
        .Where(bo => bo.User.Id == currUserId && bo.Status == OrderStatus.Open).FirstOrDefault();
      if (openOrder == null && create) {
        var user = session.GetEntity<IUser>(OpContext.User.UserId);
        openOrder = session.NewOrder(user);
      }
      return openOrder; 
    }

    #endregion

    #region Reviews
    [HttpPost, Route("reviews"), Authorize]
    public BookReview AddReview(BookReview review) {
      return CreateUpdateReview(review, create: true);
    }

    [HttpPut, Route("reviews"), Authorize]
    public BookReview UpdateReview(BookReview review) {
      return CreateUpdateReview(review, create: false);
    }

    private BookReview CreateUpdateReview(BookReview review, bool create) {
      OpContext.ThrowIfNull(review, ClientFaultCodes.ContentMissing, "Review", "Review object in message body is missing.");
      //find book
      var session = OpenSession();
      var bk = session.GetEntity<IBook>(review.BookId);

      //Validate using ValidationExtensions methods
      //will throw and return BadRequest if book Id is invalid
      OpContext.ThrowIfNull(bk, ClientFaultCodes.ObjectNotFound, "BookId", "Book not found. ID={0}", review.BookId);
      //Validate input fields
      OpContext.ValidateNotEmpty(review.Caption, "Caption", "Caption may not be empty.");
      OpContext.ValidateNotEmpty(review.Review, "Review", "Review text may not be empty.");
      OpContext.ValidateMaxLength(review.Caption, 100, "Caption", "Caption text is too long.");
      // Review text is unlimited in database, but let's still limit it to 1000 chars
      OpContext.ValidateMaxLength(review.Review, 1000, "Review", "Review text is too long, must be under 1000 chars");
      OpContext.ValidateRange(review.Rating, 1, 5, "Rating", "Rating must be between 1 and 5");
      OpContext.ThrowValidation(); //will throw if any faults had been detected; will return BadRequest with list of faults in the body 
      // get user; 
      var user = session.GetEntity<IUser>(OpContext.User.UserId);
      // with AuthenticatedOnly attribute, we should always have user; still check just in case
      OpContext.ThrowIfNull(user, ClientFaultCodes.ObjectNotFound, "User", "Current user not identified.");

      //Create/update review entity
      IBookReview entReview;
      if(create)
        entReview = session.NewReview(user, bk, review.Rating, review.Caption, review.Review);
      else {
        entReview = session.GetEntity<IBookReview>(review.Id);
        OpContext.ThrowIfNull(entReview, ClientFaultCodes.ObjectNotFound, "Review", "Review object not found, ID={0}.", review.Id);
        entReview.Caption = review.Caption;
        entReview.Rating = review.Rating;
        entReview.Review = review.Review;
      }
      session.SaveChanges();
      return entReview.ToModel(details: true);
    }

    [HttpDelete, Route("reviews/{id}")]
    public void DeleteReview(Guid id) {
      var session = OpenSession();
      var review = session.GetEntity<IBookReview>(id);
      OpContext.ThrowIfNull(review, ClientFaultCodes.ObjectNotFound, 
                "ReviewId", "Review with ID '{0}' not found.", id);
      session.DeleteEntity(review);
      session.SaveChanges();
    }
    #endregion


  }//class
}
