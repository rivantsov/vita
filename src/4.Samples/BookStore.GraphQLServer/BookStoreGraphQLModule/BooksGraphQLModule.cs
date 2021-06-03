using System;
using BookStore.GraphQLServer;
using NGraphQL.CodeFirst;

namespace BookStore.GraphQLServer {

  public class BooksGraphQLModule: GraphQLModule {

    public BooksGraphQLModule() {
      base.EnumTypes.Add(typeof(BookEdition), typeof(BookCategory), typeof(OrderStatus), typeof(UserType));
      base.ObjectTypes.Add(typeof(Book), typeof(Publisher), typeof(Author), typeof(BookReview), 
                  typeof(BookOrder), typeof(BookOrderLine), typeof(User), typeof(LoginResponse));
      base.InputTypes.Add(typeof(BookReviewInput), typeof(Paging), typeof(BookSearchInput), typeof(LoginInput));
      base.QueryType = typeof(IBookStoreQuery);
      base.MutationType = typeof(IBookStoreMutation);
      base.ResolverClasses.Add(typeof(BookStoreResolvers));

      MapEntity<IPublisher>().To<Publisher>();
      MapEntity<IBook>().To<Book>();
      MapEntity<IAuthor>().To<Author>();
      MapEntity<IBookOrder>().To<BookOrder>();
      MapEntity<IBookOrderLine>().To<BookOrderLine>();
      MapEntity<IBookReview>().To<BookReview>();
      MapEntity<IUser>().To<User>(u => new User { UserType = u.Type }); 
    }

  } //class

}
