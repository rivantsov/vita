using System;
using System.Collections.Generic;
using System.Text;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQLServer {

  public interface IBookStoreQuery {
    [GraphQLName("publishers")]
    IList<Publisher> GetPublishers();

    [GraphQLName("publisher")]
    Publisher GetPublisher(Guid id);

    IList<Book> SearchBooks(BookSearchInput search, Paging paging);

    [GraphQLName("book")]
    Book GetBook(Guid id);
    
    [GraphQLName("author")]
    Author GetAuthor(Guid id);

    User GetUser(string name); 
  }
}
