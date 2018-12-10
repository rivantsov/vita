using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Samples.BookStore.Api;

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

    public static IBookOrderLine Add(this IBookOrder order, IBook book, int quantity) {
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
      session.ScheduleUpdate<IBookOrder>(orderTotalQuery, CommandSchedule.TransactionEnd);
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


    public static SearchResults<Book> SearchBooks(this IEntitySession session, BookSearch searchParams) {
      // Warning about substring match (LIKE): Be careful using it in real apps, against big tables
      // Match by fragment results in LIKE operator which NEVER works on real volumes.
      // For MS SQL, it is OK to do LIKE with pattern that does not start with % (so it is StartsWith(smth) operator).
      //  AND column must be indexed - so server will use index. For match inside the string, LIKE is useless on big tables.
      // In our case, Title is indexed and we use StartsWith, so it's OK
      // An interesting article about speeding up string-match search in MS SQL:
      //  http://aboutsqlserver.com/2015/01/20/optimizing-substring-search-performance-in-sql-server/
      var categories = ConvertHelper.ParseEnumArray<BookCategory>(searchParams.Categories);
      var where = session.NewPredicate<IBook>()
        .AndIfNotEmpty(searchParams.Title, b => b.Title.StartsWith(searchParams.Title))
        .AndIfNotEmpty(searchParams.MaxPrice, b => b.Price <= (Decimal)searchParams.MaxPrice.Value)
        .AndIfNotEmpty(searchParams.Publisher, b => b.Publisher.Name.StartsWith(searchParams.Publisher))
        .AndIfNotEmpty(searchParams.PublishedAfter, b => b.PublishedOn.Value >= searchParams.PublishedAfter.Value)
        .AndIfNotEmpty(searchParams.PublishedBefore, b => b.PublishedOn.Value <= searchParams.PublishedBefore.Value)
        .AndIf(categories != null && categories.Length > 0, b => categories.Contains(b.Category));
      // A bit more complex clause for Author - it is many2many, results in subquery
      if(!string.IsNullOrEmpty(searchParams.AuthorLastName)) {
        var qAuthBookIds = session.EntitySet<IBookAuthor>()
          .Where(ba => ba.Author.LastName.StartsWith(searchParams.AuthorLastName))
          .Select(ba => ba.Book.Id);
        where = where.And(b => qAuthBookIds.Contains(b.Id));
      }
      // Alternative method for author name - results in inefficient query (with subquery for every row)
      //      if(!string.IsNullOrEmpty(authorLastName))
      //         where = where.And(b => b.Authors.Any(a => a.LastName == authorLastName));

      //Use VITA-defined helper method ExecuteSearch - to build query from where predicate, get total count,
      // add clauses for OrderBy, Take, Skip, run query and convert to list of model objects with TotalCount

      var results = session.ExecuteSearch(where, searchParams, ibook => ibook.ToModel(), b => b.Publisher,
          nameMapping: _orderByMapping);
      return results;
    }

    // Mapping names in BookSearch.OrderBy; the following mapping allows us to use 'pubname', which will be translated 
    // into "order by book.Publisher.Name"
    // This mapping facility allows us use more friendly names in UI code when forming search query, without thinking 
    // about exact relations between entities and property names
    static Dictionary<string, string> _orderByMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"pubname" , "Publisher.Name"}
    };



    public static bool IsSet(this BookEdition flags, BookEdition flag) {
      return (flags & flag) != 0;
    }
    public static bool IsSet(this UserType flags, UserType flag) {
      return (flags & flag) != 0;
    }
  }//class
}
