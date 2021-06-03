using System;
using System.Collections.Generic;
using System.Text;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQL {

  public interface IBookStoreQuery {
    [GraphQLName("publishers")]
    IList<Publisher> GetPublishers();

    /// <summary>Returns publisher specified by Id.</summary>
    /// <param name="id">Publisher id.</param>
    /// <returns></returns>
    [GraphQLName("publisher")]
    Publisher GetPublisher(Guid id);

    IList<Book> SearchBooks(BookSearchInput search, Paging paging);

    [GraphQLName("book")]
    Book GetBook(Guid id);
    
    [GraphQLName("author")]
    Author GetAuthor(Guid id);

    [GraphQLName("user")]
    User GetUser(string name);

    [GraphQLName("users")]
    IList<User> GetUsers(Paging paging);
  }
}
