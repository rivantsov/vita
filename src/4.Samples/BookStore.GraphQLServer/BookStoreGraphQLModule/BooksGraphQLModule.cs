using System;
using BookStore.GraphQLServer;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQLServer {

  public class BooksGraphQLModule: GraphQLModule {

    public BooksGraphQLModule() {
      base.EnumTypes.Add(typeof(BookEdition), typeof(BookCategory), typeof(OrderStatus), typeof(UserType));
      base.ObjectTypes.Add(typeof(Book), typeof(Publisher), typeof(Author), typeof(BookReview), 
                  typeof(BookOrder), typeof(BookOrderLine), typeof(User));
      base.InputTypes.Add(typeof(BookReviewInput));
      base.QueryType = typeof(IBookStoreQuery);
      base.MutationType = typeof(IBookStoreMutation);
    }

  } //class

}
