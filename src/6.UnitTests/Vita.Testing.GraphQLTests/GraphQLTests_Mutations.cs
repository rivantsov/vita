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
    public async Task TestAddReview() {
      var client = TestEnv.Client;

      // 1. Find IronMan book and a user
      var bk = await FindBook("iron");
      var user = await FindUser("cartman");

      // 2. create mutation request, variables dict
      var mutAddReview = @"
mutation ($rv: BookReviewInput!) {
  review: addReview(review: $rv) { id }
}";
      var vars = new TVars();

      // 3. First try invalid input object - expect validation errors 
      var badReviewInp = new BookReviewInput();
      vars["rv"] = badReviewInp;
      var resp = await client.PostAsync(mutAddReview, vars);
      Assert.IsTrue(resp.Errors.Count > 0, "Expected errors");

      // 4. Submit valid object, get back Id of new review
      var reviewInp = new BookReviewInput() {
        BookId = bk.Id, UserId = user.Id, Caption = "Boring", Rating = 1,
        Review = "Really really boring book."
      };
      vars["rv"] = reviewInp;
      resp = await client.PostAsync(mutAddReview, vars);
      resp.EnsureNoErrors();
      var review = resp.GetTopField<BookReview>("review");
      Assert.IsNotNull(review, "Expected review returned");

    } //method

    // helper methods
    private async Task<Book> FindBook(string title) {
      var bookQuery = @"
query ($title: String!) {
  books: searchBooks(search: {title: $title}, paging: {take: 5}) { id title  }
}";
      var vars = new TVars() { ["title"] = title };
      var resp = await TestEnv.Client.PostAsync(bookQuery, vars);
      resp.EnsureNoErrors(); 
      var books = resp.GetTopField<Book[]>("books");
      return books.First();
    }

    private async Task<User> FindUser(string userName) {
      var userQuery = @"
query ($userName: String!) {
  user(name: $userName) {id userName}
}";
      var vars = new TVars() { ["userName"] = userName };
      var resp = await TestEnv.Client.PostAsync(userQuery, vars);
      resp.EnsureNoErrors();
      var user = resp.GetTopField<User>("user");
      return user;
    }

  }
}
