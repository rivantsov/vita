using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BookStore;
using BookStore.GraphQLServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NGraphQL.Client;


namespace Vita.Testing.GraphQLTests {
  using TVars = Dictionary<string, object>;

  public partial class GraphQLTests {

    [TestMethod]
    public async Task TestAddBookReviews() {
      TestEnv.LogTestMethodStart();

      var loggedIn = await LoginUser("cartman");
      Assert.IsTrue(loggedIn, "Failed to login user cartman");

      // 1. Find a book and a user
      TestEnv.LogComment("Loading user and book objects.");
      var bk = await FindBook("three"); //three little pigs book

      // 2. create mutation request, variables dict
      var mutAddReview = @"
mutation ($rv: BookReviewInput!) {
  review: addReview(review: $rv) { id, caption, rating }
}";
      var vars = new TVars();

      // 3. First try invalid input objects - expect validation errors 
      //  3.1 - empty obj, expect 2 errors, fields Caption and Review may not be null
      //   it won't make it to resolver
      TestEnv.LogComment("Submitting invalid BookReviewInput object. Variable validator rejects it.");
      var badReviewInp = new BookReviewInput();
      vars["rv"] = badReviewInp;
      var resp = await TestEnv.PostAsync(mutAddReview, vars);
      Assert.AreEqual(2, resp.Errors.Count, "Expected 2 errors");

      //  3.2 - init Caption and Review to non-null; it will get to resolver but it will reject it, multiple problems
      TestEnv.LogComment("Submitting another invalid BookReviewInput object; input validation in resolver rejects it with multiple errors.");
      badReviewInp = new BookReviewInput() { 
        Review = "   ",  // whitespace not allowed 
        Caption = new string('x', 101) // too long
      };
      vars["rv"] = badReviewInp;
      resp = await TestEnv.PostAsync(mutAddReview, vars);
      Assert.AreEqual(4, resp.Errors.Count, "Expected 4 errors");

      // 4. Submit valid object, get back Id of new review
      TestEnv.LogComment("Submitting valid BookReviewInput object; review will be added.");
      var reviewInp = new BookReviewInput() {
        BookId = bk.Id, Caption = "Boring", Rating = 1,
        Review = "Really really boring book."
      };
      vars["rv"] = reviewInp;
      resp = await TestEnv.PostAsync(mutAddReview, vars);
      resp.EnsureNoErrors();
      var newReview = resp.GetTopField<BookReview>("review");
      Assert.IsNotNull(newReview, "Expected review returned");

      // 5. Delete the new review
      TestEnv.LogComment("Delete the review that we just created");
      var mutDelReview = @"
mutation ($reviewId: Uuid!) {
  deleteReview(reviewId: $reviewId)
}";
      vars = new TVars() { ["reviewId"] = newReview.Id };
      resp = await TestEnv.PostAsync(mutDelReview, vars);
      resp.EnsureNoErrors();

      await Logout(); 
    } //method

  }
}
