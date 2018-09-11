using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Entities;
using Vita.Entities.Api;
using System.IO;
using Vita.Entities.Utilities;
using System.Diagnostics;

namespace Vita.Samples.BookStore.Api {

  // Service controller for operations that do not require logged-in user
  public class CatalogController : BooksControllerBase {

    // Mapping names in BookSearch.OrderBy; the following mapping allows us to use 'pubname', which will be translated 
    // into "order by book.Publisher.Name"
    // This mapping facility allows us use more friendly names in UI code when forming search query, without thinking 
    // about exact relations between entities and property names
    static Dictionary<string, string> _orderByMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"pubname" , "Publisher.Name"}
    };

    /// <summary>Searches books by multiple criteria.</summary>
    /// <param name="searchParams">Search parameters object.</param>
    /// <returns>SearchResults object.</returns>
    // SearchParams object used for grabbing URL parameters into controller method. 
    // There are some problems in Web Api routing when using optional URL parameters, so such object is the easiest solution
    // when optional parameters are involved
    [ApiGet, ApiRoute("books")]
    public SearchResults<Book> SearchBooks([FromUrl] BookSearch searchParams) {
      searchParams = searchParams.DefaultIfNull(defaultOrderBy: "title"); //creates empty object if null and sets Take = 10 and default sort
      var session = OpenSession();
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

      var results = session.ExecuteSearch(where, searchParams,  ibook => ibook.ToModel(), b => b.Publisher, 
          nameMapping: _orderByMapping);
      return results;
    }

    [ApiGet, ApiRoute("books/{id}")]
    public Book GetBook(Guid id) {
      var session = OpenSession();
      var bk = session.GetEntity<IBook>(id); 
      return bk.ToModel(details: true); //if bk is null, would return null
    }

    [ApiGet, ApiRoute("authors")]
    public SearchResults<Author> SearchAuthors([FromUrl] AuthorSearch search) {
      search = search.DefaultIfNull(defaultOrderBy: "LastName");
      var session = OpenSession();
      var where = session.NewPredicate<IAuthor>()
        .AndIfNotEmpty(search.LastName, a => a.LastName.StartsWith(search.LastName));
      //books subquery
      var baPredBase = session.NewPredicate<IBookAuthor>();
      var baCond = baPredBase
        .AndIfNotEmpty(search.BookTitle, ba => ba.Book.Title.StartsWith(search.BookTitle))
        .AndIfNotEmpty(search.Category, ba => ba.Book.Category == search.Category.Value)
        .AndIfNotEmpty(search.Publisher, ba => ba.Book.Publisher.Name.StartsWith(search.Publisher));
      if(baCond != baPredBase) { //if we added any book conditions
        var baIdsSubQuery = session.EntitySet<IBookAuthor>().Where(baCond).Select(ba => ba.Book.Id).Distinct(); //returns  author IDs
        where = where.And(a => baIdsSubQuery.Contains(a.Id));
      }
      var results = session.ExecuteSearch(where, search, r => r.ToModel(details: false));
      return results;
    }

    [ApiGet, ApiRoute("authors/{id}")]
    public Author GetAuthor(Guid id) {
      var session = OpenSession();
      var auth = session.GetEntity<IAuthor>(id);
      return auth.ToModel(details: true);
    }

    [ApiGet, ApiRoute("publishers")]
    public SearchResults<Publisher> SearchPublishers([FromUrl] PublishersSearch search) {
      search = search.DefaultIfNull(defaultOrderBy: "Name");
      var session = OpenSession();
      var where = session.NewPredicate<IPublisher>()
        .AndIfNotEmpty(search.Name, p => p.Name.StartsWith(search.Name));
      var results = session.ExecuteSearch(where, search, p => p.ToModel());
      return results;
    }

    [ApiGet, ApiRoute("publishers/{id}")]
    public Publisher GetPublisher(Guid id) {
      var session = OpenSession();
      var pub = session.GetEntity<IPublisher>(id);
      return pub.ToModel();
    }

    [ApiGet, ApiRoute("reviews")]
    public SearchResults<BookReview> SearchReviews([FromUrl] ReviewSearch search) {
      search = search.DefaultIfNull(defaultOrderBy: "CreatedOn-DESC");
      var session = OpenSession();
      var where = session.NewPredicate<IBookReview>()
        .AndIfNotEmpty(search.BookId, r => r.Book.Id == search.BookId.Value)
        .AndIfNotEmpty(search.Title, r => r.Book.Title.StartsWith(search.Title))
        .AndIfNotEmpty(search.UserId, r => r.User.Id == search.UserId.Value)
        .AndIfNotEmpty(search.UserName, r => r.User.UserName.StartsWith(search.UserName))
        .AndIfNotEmpty(search.BookCategory, r => r.Book.Category == search.BookCategory.Value)
        .AndIfNotEmpty(search.PublisherId, r => r.Book.Publisher.Id == search.PublisherId.Value)
        .AndIfNotEmpty(search.MinRating, r => r.Rating >= search.MinRating.Value)
        .AndIfNotEmpty(search.MaxRating, r => r.Rating <= search.MaxRating.Value)
        .AndIfNotEmpty(search.CreatedAfter, r => r.CreatedOn >= search.CreatedAfter)
        .AndIfNotEmpty(search.CreatedBefore, r => r.CreatedOn <= search.CreatedBefore);
      var results = session.ExecuteSearch(where, search, r => r.ToModel(details: false));
      return results;
    }

    [ApiGet, ApiRoute("reviews/{id}")]
    public BookReview GetReview(Guid id) {
      var session = OpenSession();
      var review = session.GetEntity<IBookReview>(id);
      if(review == null)
        return null;
      return review.ToModel(details: true); 
    }


    //An example of returning binary stream from API controller. This end-point serves images for URLs directly
    // embedded into 'img' HTML tag. It is used for showing book cover pics
    [ApiGet, ApiRoute("images/{id}")]
    public Stream GetImage(Guid id) {
      //Do not log body
      Context.WebContext.Flags |= WebCallFlags.NoLogResponseBody; //if you end up logging it for some reason, do not log body (image itself)
      var session = OpenSession();
      session.EnableLog(false); 
      var image = session.GetEntity<IImage>(id);
      if(image == null)
        return null;
      var rec = EntityHelper.GetRecord(image);
      // Looks like media type is set automatically (and correctly)
      // Context.WebContext.OutgoingHeaders.Add("Content-Type", image.MediaType);
      var stream = new MemoryStream(image.Data);
      return stream;
    }

  }
}
