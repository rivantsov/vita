using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BookStore.GraphQL;
using NGraphQL.Client;


namespace Vita.Testing.GraphQLTests {
  using TVars = Dictionary<string, object>;

  public partial class GraphQLTests {

    private async Task<Book> FindBook(string title) {
      var bookQuery = @"
query ($title: String!) {
  books: searchBooks(search: {title: $title}, paging: {take: 5}) { id title publishedOn coverImageId }
}";
      var vars = new TVars() { ["title"] = title };
      var resp = await TestEnv.PostAsync(bookQuery, vars);
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
      var resp = await TestEnv.PostAsync(userQuery, vars);
      resp.EnsureNoErrors();
      var user = resp.GetTopField<User>("user");
      return user;
    }

  }
}
