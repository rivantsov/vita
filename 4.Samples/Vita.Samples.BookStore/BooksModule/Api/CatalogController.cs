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
      return session.SearchBooks(searchParams); 
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
