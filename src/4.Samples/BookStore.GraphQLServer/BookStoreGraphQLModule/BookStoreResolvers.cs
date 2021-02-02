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

    public IList<IBook> SearchBooks(IFieldContext context, BookSearchInput search, Paging paging) {
      // We do not use session.SearchBooks extension method defined in BookStore app, it does not quite fit;
      // we do not use explicit Include's here in pulling related entities, we rely on SmartLoad functionality.
      var where = _session.NewPredicate<IBook>()
        .AndIfNotEmpty(search.Title, b => b.Title.StartsWith(search.Title))
        .AndIfNotEmpty(search.MaxPrice, b => b.Price <= (Decimal)search.MaxPrice.Value)
        .AndIf(search.Categories != null && search.Categories.Length > 0, b => search.Categories.Contains(b.Category))
        .AndIfNotEmpty(search.Editions, b => (b.Editions & search.Editions) != 0)  //should be search.Editions.Value, but this works too; just checking bug fix
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
      paging ??= new Paging() { OrderBy = "Rating", Take = 5 };
      // We use explicit batching here
      var allParentBookIds = context.GetAllParentEntities<IBook>().Select(b => b.Id).ToList();
      var selectedReviewsByBook = SelectTopNReviewsForBooks(allParentBookIds, paging);
      var groupedReviewsByBook = selectedReviewsByBook.GroupBy(br => br.Book).ToList();
      // we have query results, list of reviews, n top for each book.
      // group them by bookId; then convert to dictionary
      // Wreviews grouped by Book_Id; to post the results into the context, 
      //  we need a dict<IBook, List<IBookReview>>
      var reviewsByBookDict = groupedReviewsByBook.ToDictionary(
        g => g.Key, g => g.ToList()
      );
      context.SetBatchedResults<IBook, List<IBookReview>>(reviewsByBookDict, valueForMissingEntry: new List<IBookReview>());
      return null; 
    }

    [ResolvesField("coverImageUrl", typeof(Book))]
    public string GetCoverImageUrl(IFieldContext context, IBook book) {
      throw new NotImplementedException();
    }

    [ResolvesField("reviews", typeof(User))]
    public IList<IBookReview> GetUserReviews(IFieldContext context, IUser user, Paging paging = null) {
      throw new NotImplementedException();
    }

    #region Reviews per book selection method
    // Selecting N reviews per book for a set of books, with specified ORDER
    /* OK, this is tricky; it can be probably done better with window function or CROSS-APPLY or smth.
    We do it with sub-select: 
    
SELECT br.[Id], br.[CreatedOn], br.[Rating], br.[Caption], br.[Review], br.[Book_Id], br.[User_Id]
  FROM [VitaBooks].[books].[BookReview] br
  WHERE br.Id in (
	SELECT TOP (2) [Id]
	  FROM [VitaBooks].[books].[BookReview]
	  WHERE Book_Id = br.Book_Id
	  Order by CreatedOn DESC  
  )
  ORDER BY CreatedOn DESC

-- Replace 'CreatedOn' in order-by with actual OrderBy list specified in Paging object. 
  Later we group the returned records by Book_id on c# side
    */
    private IList<IBookReview> SelectTopNReviewsForBooks(IList<Guid> bookIds, Paging paging) {
      var reviews = _session.EntitySet<IBookReview>();
      var reviews2 = _session.EntitySet<IBookReview>();
      var reviewQuery = reviews.Where(br =>
           bookIds.Contains(br.Book.Id) &&
           reviews2.Where(br2 => br2.Book == br.Book)
                   //.OrderBy(paging.OrderBy, null)
                   .OrderByDescending(br => br.Rating)
                   .Skip(paging.Skip).Take(paging.Take)
                   .Select(br2 => br2.Id)
                   .Contains(br.Id)
                   ); //where
      var allReviews = reviewQuery.ToList();
      return allReviews;
    }
    #endregion 


  }
}
