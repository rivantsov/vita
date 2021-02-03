using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;

namespace BookStore.GraphQLServer {
  public static class BookStoreGraphQLExtensions {

    // Converts Paging object we define in GraphQL bookStore project to SearchParams used in Vita
    // convert to searchParams that has non-nullable Skip and Take
    public static SearchParams ToSearchParams(this Paging paging, string defaultOrderBy) {
      if (paging == null)
        return new SearchParams() { OrderBy = defaultOrderBy, Take = 5 };
      var prms = new SearchParams() { OrderBy = paging.OrderBy, Skip = paging.Skip.GetValueOrDefault(), 
                                      Take = paging.Take.GetValueOrDefault(10)};
      if (string.IsNullOrWhiteSpace(prms.OrderBy))
        prms.OrderBy = defaultOrderBy;
      return prms; 
    }
  }
}
