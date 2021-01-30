﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NGraphQL.CodeFirst;
using Vita.Entities;

namespace BookStore.GraphQLServer {
  public class BookStoreResolvers: IResolverClass {
    BooksEntityApp _app;
    IEntitySession _session; 

    public void BeginRequest(IRequestContext request) {
      // _app = BooksEntityApp.Instance; //this works too
      _app = (BooksEntityApp) request.App; //general case
      _session = _app.OpenSession(); 
    }

    public void EndRequest(IRequestContext request) {
      if (_session.GetChangeCount() > 0)
        _session.SaveChanges(); 
    }

    #region Root Query methods
    [ResolvesField("publishers")] //, typeof(IBookStoreQuery))]
    public IList<IPublisher> GetPublishers(IFieldContext context) {
      return _session.EntitySet<IPublisher>().OrderBy(p => p.Name).ToList();
    }

    public IPublisher GetPublisher(IFieldContext context, Guid id) {
      return _session.GetEntity<IPublisher>(id); 
    }

    public IList<IBook> SearchBooks(IFieldContext context, BookSearch search, Paging paging) {
      throw new NotImplementedException();
    }

    public IList<IBook> SearchAuthors(IFieldContext context, AuthorSearch search, Paging paging) {
      throw new NotImplementedException();
    }

    public IBook GetBook(IFieldContext context, Guid id) {
      return _session.GetEntity<IBook>(id); 
    }

    public IAuthor GetAuthor(IFieldContext context, Guid id) {
      return _session.GetEntity<IAuthor>(id);
    }

    public IUser GetUser(IFieldContext context, string name) {
      return _session.EntitySet<IUser>().FirstOrDefault(u => u.UserName == name);
    }
    #endregion

    #region Root mutation methods
    public IBookOrder CreateOrder(IFieldContext context, Guid userId) {
      throw new NotImplementedException();
    }

    public IBookOrderLine AddOrderItem(IFieldContext context, Guid orderId, Guid bookId, int count = 1) {
      throw new NotImplementedException();
    }

    public bool RemoveOrderItem(IFieldContext context, Guid itemId) {
      throw new NotImplementedException();
    }

    public IBookOrder SubmitOrder(IFieldContext context, Guid orderId) {
      throw new NotImplementedException();
    }

    public IBookReview AddReview(IFieldContext context, BookReviewInput review) {
      throw new NotImplementedException();
    }
    #endregion

    [ResolvesField("reviews", typeof(Book))]
    public IList<IBookReview> GetBookReviews(IFieldContext context, IBook book, Paging paging = null) {
      throw new NotImplementedException();
    }

    [ResolvesField("coverImageUrl", typeof(Book))]
    public string GetCoverImageUrl(IFieldContext context, IBook book) {
      throw new NotImplementedException();
    }

    [ResolvesField("reviews", typeof(User))]
    public IList<IBookReview> GetUserReviews(IFieldContext context, IUser user, Paging paging = null) {
      throw new NotImplementedException();
    }

    /*
    [ResolvesField("books", typeof(Author))]
    public IList<IBook> GetAuthorBooks(IFieldContext context, IAuthor author, Paging paging = null) {
      return author.Books;
    }
    [ResolvesField("books", typeof(Publisher))]
    public IList<IBook> GetPublisherBooks(IFieldContext context, IPublisher publisher, Paging paging = null) {
      return publisher.Books;
    }

    */



  }
}