using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BookStore.GraphQL;
using NGraphQL.Client;


namespace Vita.Testing.GraphQLTests {
  using TVars = Dictionary<string, object>;

  public partial class GraphQLTests {

    private async Task<bool> LoginUser(string userName, string password = null) {
      password ??= BookStore.SampleData.SampleDataGenerator.DefaultPassword;
      var mutLogin = @"
mutation ($lg: LoginInput!) {
  loginUser(login: $lg) { userId, userName, token }
}";
      var vars = new TVars();
      vars["lg"] = new LoginInput() { UserName = userName, Password = password };
      var resp = await TestEnv.Client.PostAsync(mutLogin, vars);
      if (resp.Errors?.Count > 0) {
        var errors = resp.GetErrorsAsText();
        var errText = $"Login failed for user {userName}.\r\n {errors}";
        Debug.WriteLine(errText);
        TestEnv.LogComment(errText);
        return false;
      }
      TestEnv.LogComment($"Login succeeded for user {userName}.");
      var loginResp = resp.GetTopField<LoginResponse>("loginUser");
      var authToken = loginResp.Token;
      TestEnv.Client.AddAuthorizationHeader(authToken); //add token to auth header
      return true;
    }

    private async Task Logout() {
      var mutLogout = @"
mutation {
  logout
}";
      var resp = await TestEnv.Client.PostAsync(mutLogout);
      TestEnv.Client.DefaultRequestHeaders.Authorization = null;
    }

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
