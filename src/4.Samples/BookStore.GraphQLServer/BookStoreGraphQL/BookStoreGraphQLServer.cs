using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NGraphQL.Server;

namespace BookStore.GraphQL {

  public class BookStoreGraphQLServer: GraphQLServer {
    public BookStoreGraphQLServer(BooksEntityApp app): base(app) {
      // Important: for VITA projects and DB projects in general, do NOT enable parallel execution;
      // it will negatively impact throughput of the app (multiple DB connections reserved for single request). 
      base.Settings.Options = GraphQLServerOptions.EnableRequestCache | GraphQLServerOptions.ReturnExceptionDetails;
      RegisterModules(new BooksGraphQLModule());
    }
  }
}
