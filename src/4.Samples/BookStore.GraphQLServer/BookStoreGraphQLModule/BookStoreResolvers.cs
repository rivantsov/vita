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
      var searchResults = _session.ExecuteSearch(where, paging.ToSearchParams("Title"));
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

    public IList<IUser> GetUsers(IFieldContext context, Paging paging = null) {
      var prms = paging.ToSearchParams("UserName");
      return _session.EntitySet<IUser>().OrderBy(paging.OrderBy)
                          .Skip(prms.Skip).Take(prms.Take).ToList();
    }
    #endregion

    #region Root mutation methods
    public IBookReview AddReview(IFieldContext context, BookReviewInput review) {
      var book = _session.GetEntity<IBook>(review.BookId);
      var user = _session.GetEntity<IUser>(review.UserId); 
      context.AddErrorIf(book == null, "Invalid book Id, book not found.");
      context.AddErrorIf(user == null, "Invalid user Id, user not found.");
      context.AddErrorIf(string.IsNullOrWhiteSpace(review.Caption), "Caption may not be empty");
      context.AddErrorIf(string.IsNullOrWhiteSpace(review.Review), "Review text may not be empty");
      context.AddErrorIf(review.Caption != null && review.Caption.Length > 100, 
                     "Caption is too long, must be less than 100 chars.");
      context.AddErrorIf(review.Rating < 1 || review.Rating > 5,
                    $"Invalid rating value ({review.Rating}), must be between 1 and 5");
      context.AbortIfErrors();
      var reviewEnt = _session.NewReview(user, book, review.Rating, review.Caption, review.Review);
      // changes will be saved when request completes (see EndRequest method)
      return reviewEnt; 
    }

    public bool DeleteReview(IFieldContext context, Guid reviewId) {
      var review = _session.GetEntity<IBookReview>(reviewId);
      context.AbortIf(review == null, "Invalid review ID, review not found.");
      _session.DeleteEntity(review);
      return true; 
    }
    #endregion

    [ResolvesField("reviews", typeof(Book))]
    public IList<IBookReview> GetBookReviews(IFieldContext context, IBook book, Paging paging = null) {
      // The method is called to return reviews for a single book. To avoid N+1 problem, we retrieve reviews for ALL books in this call -
      // for books that are returned at the parent level, and for which we expect to be called again.
      // We get the list of all these books, produce the list of their Ids, query the database to get their reviews,
      //  make a dictionary <bookId, reviews> and then post this dict into the field context. 
      // The engine will not invoke this resolver again. 
      paging ??= new Paging() { OrderBy = "createdOn-desc", Take = 5 };
      // We use explicit batching here
      var allBookIds = context.GetAllParentEntities<IBook>().Select(b => b.Id).ToList();
      // call helper method that does some magic query
      var selectedReviewsByBook = SelectReviewsByBookPaged(allBookIds, paging);
      var groupedReviewsByBook = selectedReviewsByBook.GroupBy(br => br.Book).ToList();
      // we have query results, list of reviews, n for each book according to paging parameter.
      // Put them into dictionary and post into context.  
      var reviewsByBookDict = groupedReviewsByBook.ToDictionary(g => g.Key, g => g.ToList());
      // valueForMissingKeys is a value to use when bookId is not in a dictionary. Note that dict contains only entries for books
      // that have reviews. 
      context.SetBatchedResults<IBook, List<IBookReview>>(reviewsByBookDict, valueForMissingKeys: new List<IBookReview>());
      return null; // engine will use the posted dictionary to get this value. 
    }

    // for comments see similar GetBookReviews method above
    [ResolvesField("reviews", typeof(User))]
    public IList<IBookReview> GetUserReviews(IFieldContext context, IUser user, Paging paging = null) {
      paging ??= new Paging() { OrderBy = "createdOn-desc", Take = 5 };
      var allUserIds = context.GetAllParentEntities<IUser>().Select(u => u.Id).ToList();
      // call helper method that does some magic query
      var selectedReviewsByUser = SelectReviewsByUserPaged(allUserIds, paging);
      var groupedReviewsByUser = selectedReviewsByUser.GroupBy(br => br.User).ToList();
      var reviewsByUserDict = groupedReviewsByUser.ToDictionary(g => g.Key, g => g.ToList());
      context.SetBatchedResults<IUser, List<IBookReview>>(reviewsByUserDict, valueForMissingKeys: new List<IBookReview>());
      return null; // engine will use the posted dictionary to get this value. 
    }

    [ResolvesField("orders", typeof(User))]
    public List<IBookOrder> GetUserOrders(IFieldContext context, IUser user, Paging paging = null) {
      paging ??= new Paging() { OrderBy = "createdOn-desc", Take = 5 };
      var allUserIds = context.GetAllParentEntities<IUser>().Select(u => u.Id).ToList();
      // call helper method that does some magic query
      var selectedOrdersByUser = SelectOrdersByUserPaged(allUserIds, paging);
      var groupedOrdersByUser = selectedOrdersByUser.GroupBy(br => br.User).ToList();
      var ordersByUserDict = groupedOrdersByUser.ToDictionary(g => g.Key, g => g.ToList());
      context.SetBatchedResults<IUser, List<IBookOrder>>(ordersByUserDict, valueForMissingKeys: new List<IBookOrder>());
      return null; // engine will use the posted dictionary to get this value. 
    }


    #region Reviews per book selection method
    // Selecting N reviews per book for a set of books, with specified ORDER, skip, take
    /* OK, this is tricky; it can be probably done better with window function or CROSS-APPLY or smth.
    We do it with sub-select: 
    
SELECT br.[Id], br.[CreatedOn], br.[Rating], br.[Caption], br.[Review], br.[Book_Id], br.[User_Id]
  FROM [books].[BookReview] br
  WHERE br.[Id] IN ( <book id list> ) AND br.Id in (
	  SELECT [Id]
	    FROM [books].[BookReview]
	    WHERE Book_Id = br.Book_Id
	    ORDER by CreatedOn DESC 
      OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY
    )
  ORDER BY CreatedOn DESC

  Later we group the returned records by Book_id on c# side;
  The following code builds LINQ expressions that result in SQL above
    */

    private IList<IBookReview> SelectReviewsByBookPaged(IList<Guid> allBookIds, Paging paging) {
      var reviewsBaseQuery = _session.EntitySet<IBookReview>().Where(br => allBookIds.Contains(br.Book.Id));
      var page = paging.ToSearchParams("createdOn-desc"); 
      // Limitation, to be fixed; this OrderBy(string) is a special method defined in VITA, 
      // and it should be executed directly against entity set, not inside bigger expression as subquery.
      var subQuery = _session.EntitySet<IBookReview>().OrderBy(paging.OrderBy, null).Skip(page.Skip).Take(page.Take); 
      var query = reviewsBaseQuery.Where(br =>
               subQuery.Where(br2 => br2.Book == br.Book).Contains(br)); 
      var allReviews = query.ToList();
      return allReviews;
    }

    private IList<IBookReview> SelectReviewsByUserPaged(IList<Guid> userIds, Paging paging) {
      var page = paging.ToSearchParams("createdOn-desc"); 
      var reviewsBaseQuery = _session.EntitySet<IBookReview>().Where(br => userIds.Contains(br.User.Id));
      var subQuery = _session.EntitySet<IBookReview>().OrderBy(page.OrderBy, null).Skip(page.Skip).Take(page.Take);
      var query = reviewsBaseQuery.Where(br =>
               subQuery.Where(br2 => br2.User == br.User).Contains(br)); 
      var reviews = query.ToList();
      return reviews;
    }

    private IList<IBookOrder> SelectOrdersByUserPaged(IList<Guid> userIds, Paging paging) {
      var page = paging.ToSearchParams("createdOn-desc");
      var ordersBaseQuery = _session.EntitySet<IBookOrder>().Where(ord => userIds.Contains(ord.User.Id));
      var subQuery = _session.EntitySet<IBookOrder>().OrderBy(page.OrderBy, null).Skip(page.Skip).Take(page.Take);
      var query = ordersBaseQuery.Where(ord =>
               subQuery.Where(ord2 => ord2.User == ord.User).Contains(ord)); 
      var orders = query.ToList();
      return orders;
    }
    #endregion 


  }
}
