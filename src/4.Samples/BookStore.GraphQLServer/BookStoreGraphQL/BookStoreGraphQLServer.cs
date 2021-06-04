using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NGraphQL.Server;

using Vita.Entities;

namespace BookStore.GraphQL {

  public class BookStoreGraphQLServer: GraphQLServer {
    public BookStoreGraphQLServer(BooksEntityApp app): base(app) {
      // Important: for VITA projects and DB projects in general, do NOT enable parallel execution;
      // it will negatively impact throughput of the app (multiple DB connections reserved for single request). 
      base.Settings.Options = GraphQLServerOptions.EnableRequestCache | GraphQLServerOptions.ReturnExceptionDetails;
      RegisterModules(new BooksGraphQLModule());
      base.Events.OperationError += Events_OperationError;
    }

    private static void Events_OperationError(object sender, OperationErrorEventArgs args) {
      // When errors are detected in SaveChanges, the fwk throws ClientFaultExc with multiple 
      //  errors inside: for ex, 'Value too long', 'Value missing' etc. Here we convert each fault
      //  to GraphQL Error object in response. 
      if (args.Exception is ClientFaultException cfEx) {
        var ctx = args.RequestContext;
        foreach (var fault in cfEx.Faults) {
          ctx.AddError(fault.Message, args.RequestItem, errorType: fault.Code);
        }
        // clear original exc
        args.ClearException();
      }
    }

  }
}
