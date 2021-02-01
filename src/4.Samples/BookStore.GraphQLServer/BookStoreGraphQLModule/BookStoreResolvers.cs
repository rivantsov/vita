using System;
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
      _session = _app.OpenSession(EntitySessionOptions.EnableSmartLoad); 
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

    /*
    private IList<IBook> SearchBooks_Disabled(IFieldContext context, BookSearchInput search, Paging paging) {
      // We use session.SearchBooks extension method defined in BookStore app. 
      // We need to convert our 'search' and 'paging' args to BookSearch object used by this method 
      // (BookSearch combines search values with paging params)
      var categoriesString = search.Categories == null ? null : string.Join(",", search.Categories);
      var bookSearch = new BookSearch() {
        Title = search.Title, AuthorLastName = search.AuthorLastName, Categories = categoriesString,
        MaxPrice = search.MaxPrice, PublishedAfter = search.PublishedAfter, Publisher = search.Publisher,
        OrderBy = paging.OrderBy, Skip = paging.Skip, Take = paging.Take
      };
      var searchResults = _session.SearchBooks(bookSearch);
      return searchResults.Results; 
    }
    */

    public IList<IBook> SearchBooks(IFieldContext context, BookSearchInput search, Paging paging) {
      // We could use session.SearchBooks extension method defined in BookStore app, but it does not quite fit;
      // we do not use explicit Include's here in pulling related entities, we rely on SmartLoad functionality. 

      var where = _session.NewPredicate<IBook>()
        .AndIfNotEmpty(search.Title, b => b.Title.StartsWith(search.Title))
        .AndIfNotEmpty(search.MaxPrice, b => b.Price <= (Decimal)search.MaxPrice.Value)
        .AndIf(search.Categories != null && search.Categories.Length > 0, b => search.Categories.Contains(b.Category))
        .AndIfNotEmpty(search.Editions, b => (b.Editions & search.Editions) != 0)  //should be search.Editions.Value, but this works too
        .AndIfNotEmpty(search.Publisher, b => b.Publisher.Name.StartsWith(search.Publisher))
        .AndIfNotEmpty(search.PublishedAfter, b => b.PublishedOn.Value >= search.PublishedAfter.Value)
        .AndIfNotEmpty(search.PublishedBefore, b => b.PublishedOn.Value <= search.PublishedBefore.Value);
      // A bit more complex clause for Author - it is many2many, results in subquery
      if (!string.IsNullOrEmpty(search.AuthorLastName)) {
        var qAuthBookIds = _session.EntitySet<IBookAuthor>()
          .Where(ba => ba.Author.LastName.StartsWith(search.AuthorLastName))
          .Select(ba => ba.Book.Id);
        where = where.And(b => qAuthBookIds.Contains(b.Id));
      }
      var searchResults = _session.ExecuteSearch(where, paging.ToSearchParams());
      return searchResults.Results;
    }


    public IList<IBook> SearchAuthors(IFieldContext context, AuthorSearchInput search, Paging paging) {
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
