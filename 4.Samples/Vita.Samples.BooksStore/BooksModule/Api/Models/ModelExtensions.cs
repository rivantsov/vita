using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Samples.BookStore.Api {

  public static class ModelExtensions {
    public static Book ToModel(this IBook book, bool details = false) {
      if(book == null)
        return null; 
      var bookDto = new Book() { 
        Id = book.Id, Publisher = book.Publisher.ToModel(), Title = book.Title, Price = book.Price,
        PublishedOn = book.PublishedOn, Category = book.Category, Editions = book.Editions,
        Authors = book.Authors.Select(a => a.ToModel(false)).ToList()
      };
      if(book.CoverImage != null)
        bookDto.CoverImageId = book.CoverImage.Id; 
      if(details) {
        bookDto.Description = book.Description;
        bookDto.Abstract = book.Abstract;
        bookDto.LatestReviews = GetLatestReviews(book, 5); 
      }
      return bookDto; 
    }

    public static List<BookReview> GetLatestReviews(IBook book, int take = 10) {
      var session = EntityHelper.GetSession(book);
      var rvSet = session.EntitySet<IBookReview>();
      var query = from rv in rvSet
                  where rv.Book == book 
                  orderby rv.CreatedOn descending
                  select new BookReview() {Id = rv.Id, BookId = rv.Book.Id, Caption = rv.Caption, 
                                             CreatedOn = rv.CreatedOn, Rating = rv.Rating, Review = rv.Review,
                                             UserName = rv.User.DisplayName};
      var reviews = query.Take(take).ToList();
      return reviews; 
    }

    public static Publisher ToModel(this IPublisher publisher) {
      if(publisher == null)
        return null; 
      return new Publisher() { Id = publisher.Id, Name = publisher.Name };
    }

    public static Author ToModel(this IAuthor author, bool details = false) {
      if(author == null)
        return null; 
      var auth = new Author() { Id = author.Id, FirstName = author.FirstName, LastName = author.LastName};
      if(details)
        auth.Bio = author.Bio;
      return auth; 
    }

    public static BookReview ToModel(this IBookReview review, bool details = false) {
      if(review == null)
        return null; 
      var m = new BookReview() {
        Id = review.Id, CreatedOn = review.CreatedOn, BookId = review.Book.Id, UserName = review.User.DisplayName,
        Rating = review.Rating, Caption = review.Caption
      };
      if(details)
        m.Review = review.Review;
      return m; 
    }

    public static BookOrder ToModel(this IBookOrder order, bool details = false) {
      if(order == null) 
        return null;
      var ord = new BookOrder() {
        Id = order.Id, CreatedOn = order.CreatedOn, Total = order.Total, Status = order.Status,  UserId = order.User.Id, UserName = order.User.DisplayName
      };
      if(details)
        ord.Items = order.Lines.Select(l => l.ToModel()).ToList();
      return ord;
    }

    public static BookOrderItem ToModel(this IBookOrderLine line) {
      if(line == null)
        return null;
      var item = new BookOrderItem() {
        Id = line.Id, OrderId = line.Order.Id, Book = line.Book.ToModel(), Quantity = line.Quantity
      };
      return item; 
    }

    public static User ToModel(this IUser user) {
      if(user == null)
        return null;
      return new User() { Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName };

    }

  } //class
}//ns
