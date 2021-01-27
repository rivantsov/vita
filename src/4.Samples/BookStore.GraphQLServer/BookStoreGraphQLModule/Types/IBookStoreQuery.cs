using System;
using System.Collections.Generic;
using System.Text;

namespace BookStore.GraphQLServer {

  public interface IBookStoreQuery {
    IList<Publisher> Publishers();
    Publisher Publisher(Guid id);
    IList<Book> SearchBooks(BookSearch search, Paging paging);
    IList<Book> SearchAuthors(AuthorSearch search, Paging paging);
    Book Book(Guid id);
    Author Author(Guid id); 

    User GetUser(string name); 
  }
}
