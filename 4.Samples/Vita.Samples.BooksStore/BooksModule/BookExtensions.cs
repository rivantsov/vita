using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Common;

using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Modules.JobExecution;

namespace Vita.Samples.BookStore {
  //Helper methods to create entities
  public static class BookExtensions {

    public static UserInfo ToUserInfo(this IUser user) {
      return new UserInfo(user.Id, user.UserName);
    }
    public static IBook NewBook(this IEntitySession session, BookEdition editions, BookCategory category, string title, string description,
         IPublisher publisher, DateTime? publishedOn, decimal price, IImage coverImage = null) {
      var book = session.NewEntity<IBook>();
      book.Editions = editions;
      book.Category = category; 
      book.Title = title;
      book.Description = description;
      book.Publisher = publisher;
      book.PublishedOn = publishedOn;
      book.Price = price;
      book.CoverImage = coverImage; 
      return book;
    }

    public static IPublisher NewPublisher(this IEntitySession session, string name) {
      var pub = session.NewEntity<IPublisher>();
      pub.Name = name;
      return pub;
    }

    public static IAuthor NewAuthor(this IEntitySession session, string firstName, string lastName, string bio = null) {
      var auth = session.NewEntity<IAuthor>();
      auth.FirstName = firstName;
      auth.LastName = lastName;
      auth.Bio = bio;// ?? string.Empty; //experiment/behavior check
      return auth;
    }

    public static IUser NewUser(this IEntitySession session, string userName, UserType type, string displayName = null) {
      var user = session.NewEntity<IUser>();
      user.UserName = userName;
      user.DisplayName =  string.IsNullOrWhiteSpace(displayName) ? userName : displayName;
      user.Type = type;
      user.IsActive = true; 
      return user; 
    }

    public static ICoupon NewCoupon(this IEntitySession session, string promoCode, double discountPerc, DateTime expires) {
      var coupon = session.NewEntity<ICoupon>();
      coupon.PromoCode = promoCode;
      coupon.DiscountPerc = discountPerc;
      coupon.ExpiresOn = expires;
      return coupon;
    }

    public static IBookOrder NewOrder(this IEntitySession session, IUser user) {
      var order = session.NewEntity<IBookOrder>();
      order.User = user;
      order.Status = OrderStatus.Open;
      return order;
    }

    public static IBookOrderLine Add(this IBookOrder order, IBook book, byte quantity) {
      var session = EntityHelper.GetSession(order);
      var line = session.NewEntity<IBookOrderLine>();
      line.Order = order; 
      line.Book = book;
      line.Quantity = quantity; 
      line.Price = book.Price;
      order.Lines.Add(line); 
      return line; 
    }

    public static void CompleteOrder(this IBookOrder order, string couponCode = null) {
      var session = EntityHelper.GetSession(order);
      order.Total = order.Lines.Sum(line => line.Price * line.Quantity);
      if (!string.IsNullOrWhiteSpace(couponCode)) {
        var entCoupon = LookupCoupon(session, couponCode);
        session.Context.ThrowIfNull(entCoupon, ClientFaultCodes.ObjectNotFound, "Coupon", "Coupon with code '{0}' not found.", couponCode);
        if (entCoupon != null && entCoupon.ExpiresOn >= DateTime.Now && entCoupon.AppliedOn == null) {
          entCoupon.AppliedOn = DateTime.Now;
          order.Total = (decimal) (((double)order.Total) * ((100 - entCoupon.DiscountPerc) / 100.0));
        }
      }
      order.Status = OrderStatus.Completed;
    }

    //Schedules update LINQ-based command that will recalculate totals at the end of SaveChanges transaction
    public static void ScheduleUpdateTotal(this IBookOrder order) {
      // only open orders; completed order might have coupon discount applied
      if(order.Status != OrderStatus.Open)
        return; 
      var session = EntityHelper.GetSession(order);
      var orderTotalQuery = from bol in session.EntitySet<IBookOrderLine>()
                              where bol.Order.Id == order.Id 
                              group bol by bol.Order.Id into orderUpdate
                              select new { Id = orderUpdate.Key, Total = orderUpdate.Sum(line => line.Price * line.Quantity) };
      session.ScheduleNonQuery<IBookOrder>(orderTotalQuery, LinqCommandType.Update, CommandSchedule.TransactionEnd);
    }

    public static IBookReview NewReview(this IEntitySession session, IUser user, IBook book, int rating, string caption, string text) {
      var review = session.NewEntity<IBookReview>();
      review.User = user;
      review.Book = book;
      review.Rating = rating; 
      review.Caption = caption;
      review.Review = text;
      return review;
    }

    public static IImage NewImage(this IEntitySession session, string name, ImageType type, string mediaType, byte[] data) {
      var img = session.NewEntity<IImage>();
      img.Name = name;
      img.Type = type;
      img.MediaType = mediaType;
      img.Data = data;
      return img; 
    }

    public static ICoupon LookupCoupon(this IEntitySession session, string code) {
      var query = from c in session.EntitySet<ICoupon>()
                   where c.PromoCode == code
                   select c;
      var coupon = query.FirstOrDefault();
      return coupon; 
    }



    public static bool IsSet(this BookEdition flags, BookEdition flag) {
      return (flags & flag) != 0;
    }
    public static bool IsSet(this UserType flags, UserType flag) {
      return (flags & flag) != 0;
    }
  }//class
}
