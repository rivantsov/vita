using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NGraphQL.CodeFirst;
using Vita.Entities;

namespace BookStore.GraphQLServer {
  public class BookStoreResolvers: IResolverClass {
    BooksEntityApp _app;
    IEntitySession _session; 

    public void BeginRequest(IRequestContext request) {
      // _app = BooksEntityApp.Instance; //this works too
      _app = (BooksEntityApp) request.App; //general case
      _session = _app.OpenSession(); 
    }

    public void EndRequest(IRequestContext request) {
      if (_session.GetChangeCount() > 0)
        _session.SaveChanges(); 
    }

    #region Root Query methods
    [ResolvesField("publishers")] //, typeof(IBookStoreQuery))]
    public IList<IPublisher> GetPublishers(IFieldContext context) {
      return new List<IPublisher>();
    }

    public IPublisher GetPublisher(IFieldContext context, Guid id) {
      throw new NotImplementedException();
    }

    public IList<IBook> SearchBooks(IFieldContext context, BookSearch search, Paging paging) {
      throw new NotImplementedException();
    }

    public IList<IBook> SearchAuthors(IFieldContext context, AuthorSearch search, Paging paging) {
      throw new NotImplementedException();
    }

    public IBook GetBook(IFieldContext context, Guid id) {
      throw new NotImplementedException();
    }

    public IAuthor GetAuthor(IFieldContext context, Guid id) {
      throw new NotImplementedException();
    }

    public IUser GetUser(IFieldContext context, string name) {
      throw new NotImplementedException();
    }
    #endregion

  }
}
