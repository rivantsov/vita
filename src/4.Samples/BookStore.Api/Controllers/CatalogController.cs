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
using Microsoft.AspNetCore.Mvc;
using Vita.Web;
using Microsoft.AspNetCore.Http;

namespace BookStore.Api {

  // Service controller for operations that do not require logged-in user
  [Route("api")]
  public class CatalogController : BaseApiController {

    /// <summary>Searches books by multiple criteria.</summary>
    /// <param name="searchParams">Search parameters object.</param>
    /// <returns>SearchResults object.</returns>
    // SearchParams object used for grabbing URL parameters into controller method. 
    // There are some problems in Web Api routing when using optional URL parameters, so such object is the easiest solution
    // when optional parameters are involved
    [HttpGet, Route("books")]
    public SearchResults<Book> SearchBooks([FromQuery] BookSearch searchParams) {
      searchParams = searchParams.DefaultIfNull(defaultOrderBy: "title"); //creates empty object if null and sets Take = 10 and default sort
      var session = OpenSession();
      // Original method returns results with IBook entities 
      var tempResults = session.SearchBooks(searchParams);
      // convert to Book dto objects 
      var results = new SearchResults<Book>() {
          TotalCount = tempResults.TotalCount,
          Results = tempResults.Results.Select(b => b.ToModel()).ToList() };
      return results; 
    }

    [HttpGet, Route("books/{id}")]
    public Book GetBook(Guid id) {
      var session = OpenSession();
      var bk = session.GetEntity<IBook>(id); 
      return bk.ToModel(details: true); //if bk is null, would return null
    }

    [HttpGet, Route("authors")]
    public SearchResults<Author> SearchAuthors([FromQuery] AuthorSearch search) {
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

    [HttpGet, Route("authors/{id}")]
    public Author GetAuthor(Guid id) {
      var session = OpenSession();
      var auth = session.GetEntity<IAuthor>(id);
      return auth.ToModel(details: true);
    }

    [HttpGet, Route("publishers")]
    public SearchResults<Publisher> SearchPublishers([FromQuery] PublishersSearch search) {
      search = search.DefaultIfNull(defaultOrderBy: "Name");
      var session = OpenSession();
      var where = session.NewPredicate<IPublisher>()
        .AndIfNotEmpty(search.Name, p => p.Name.StartsWith(search.Name));
      var results = session.ExecuteSearch(where, search, p => p.ToModel());
      return results;
    }

    [HttpGet, Route("publishers/{id}")]
    public Publisher GetPublisher(Guid id) {
      var session = OpenSession();
      var pub = session.GetEntity<IPublisher>(id);
      return pub.ToModel();
    }

    [HttpGet, Route("reviews")]
    public SearchResults<BookReview> SearchReviews([FromQuery] ReviewSearch search) {
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

    [HttpGet, Route("reviews/{id}")]
    public BookReview GetReview(Guid id) {
      var session = OpenSession();
      var review = session.GetEntity<IBookReview>(id);
      if(review == null)
        return null;
      return review.ToModel(details: true); 
    }


    //An example of returning binary stream from API controller. This end-point serves images for URLs directly
    // embedded into 'img' HTML tag. It is used for showing book cover pics
    [HttpGet, Route("images/{id}")]
    public Stream GetImage(Guid id) {
      //Do not log body
      OpContext.WebContext.Flags |= WebCallFlags.NoLogResponseBody; //if you end up logging it for some reason, do not log body (image itself)
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
