using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Samples.BookStore.Api;
using Arrest;
using Arrest.Sync;
using Vita.Samples.BookStore;
using System.Globalization;

namespace Vita.UnitTests.Web {

  public partial class WebTests  {

    [TestMethod]
    public void TestCatalogFunctions() {
      var client = Startup.Client;

      //Get all books
      var allBooks = client.Get<SearchResults<Book>>("api/books?take={0}", 10);
      Assert.IsTrue(allBooks.TotalCount > 3);
      var csBk = allBooks.Results.First(bk => bk.Title.StartsWith("c#"));
      //get book details
      var csBkDet = client.Get<Book>("api/books/{0}", csBk.Id);
      Assert.IsNotNull(csBkDet, "Failed to get c# book details.");
      //check latest reviews are returned
      Assert.IsTrue(csBkDet.LatestReviews.Count > 0, "Reviews not returned");

      // search with multiple terms - should return c# book
      //Let's make a specific search that should bring c# book.
      var bks = client.Get<SearchResults<Book>>(
        "api/books?title={0}&categories={1}&maxprice={2}&publisher={3}&publishedafter={4}&publishedbefore={5}&authorlastname={6}" +
        "&orderby={7}&skip={8}&take={9}",
        "c#", "Programming", 100.0, "MS Books", "2001/02/03", DateTime.Now.Date, "Sharp", "PublishedOn-desc", 0, 10);
      Assert.AreEqual(1, bks.TotalCount, "Should return single c# book");
      Assert.AreEqual(1, bks.Results.Count, "Should return single c# book");
      Assert.AreEqual("c# Programming", bks.Results[0].Title, "Expected c# book title.");

      // another way to do the same - formatting URL query from SearchObject
      var searchParams = new BookSearch() {
        Title = "c#", Categories = "Programming", MaxPrice = 100.0, Publisher = "MS Books",
        PublishedAfter = new DateTime(2001, 1, 1), PublishedBefore = DateTime.Now.Date,
        AuthorLastName = "Sharp", OrderBy = "PublishedOn-desc", Skip = 0, Take = 10
      };

      var bkQuery = client.BuildUrlQuery(searchParams);
      bks = client.Get<SearchResults<Book>>("api/books?" + bkQuery);
      Assert.AreEqual(1, bks.TotalCount, "Should return single c# book");
      Assert.AreEqual(1, bks.Results.Count, "Should return single c# book");
      Assert.AreEqual("c# Programming", bks.Results[0].Title, "Expected c# book title.");

      //Authors
      var authRes = client.Get<SearchResults<Author>>("api/authors?lastname=sharp");
      Assert.AreEqual(1, authRes.Results.Count, "expected 1 author");
      Assert.AreEqual("Sharp", authRes.Results[0].LastName, "Expected John Sharp.");
      // author by id
      var authId = authRes.Results[0].Id;
      var authSharp = client.Get<Author>("api/authors/{0}", authId);
      Assert.IsNotNull(authSharp, "Expected author");
      Assert.IsTrue(!string.IsNullOrWhiteSpace(authSharp.Bio), "Expected Bio for author Sharp");

      //Publishers 
      var pubRes = client.Get<SearchResults<Publisher>>("api/publishers?name=ms");
      Assert.AreEqual(1, pubRes.Results.Count, "expected 1 publisher");
      Assert.AreEqual("MS Books", pubRes.Results[0].Name, "Expected MS Books.");
      // publisher by id
      var pubId = pubRes.Results[0].Id;
      var pubMs = client.Get<Publisher>("api/publishers/{0}", pubId);
      Assert.IsNotNull(pubMs, "Expected MS Books");

      // Reviews - let's find Dora's review
      var reviewsRes = client.Get<SearchResults<BookReview>>("api/reviews?username=do&title=c&createdafter=2000-01-02&minrating=4");
      Assert.AreEqual(1, reviewsRes.Results.Count, "expected 1 review");
      Assert.AreEqual("Very interesting book!", reviewsRes.Results[0].Caption, "Expected 'Very interesting..' review.");
      // review by id
      var reviewId = reviewsRes.Results[0].Id;
      var doraReview = client.Get<BookReview>("api/reviews/{0}", reviewId);
      Assert.IsNotNull(doraReview, "Expected Dora review");
      // Get-by-id returns details, so Review prop should not be empty
      Assert.IsTrue(!string.IsNullOrWhiteSpace(doraReview.Review), "Expected Review not empty.");
    } // test method

    [TestMethod]
    public void TestGetImage() {
      var client = Web.Startup.Client;

      //Get c# book
      var searchBooks = client.Get<SearchResults<Book>>("api/books?title=c#");
      Assert.AreEqual(1, searchBooks.Results.Count, "Expected c# book only.");
      var csBook = searchBooks.Results[0];
      var imageUrl = client.Settings.ServiceUrl + "/api/images/" + csBook.CoverImageId;
      var bytes = client.GetBinary(imageUrl, null, acceptMediaType: "image/jpeg");
      Assert.IsNotNull(bytes, "Expected image bytes");
      //Jpeg starts with 0xFFD8
      Assert.AreEqual(0xFF, bytes[0], "Expected FF byte.");
      Assert.AreEqual(0xD8, bytes[1], "Expected D8 byte.");
    }
  }//class
}
