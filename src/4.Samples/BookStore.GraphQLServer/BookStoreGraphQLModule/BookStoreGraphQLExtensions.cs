using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;

namespace BookStore.GraphQLServer {
  public static class BookStoreGraphQLExtensions {
    
    public static SearchParams ToSearchParams(this Paging paging) {
      if (paging == null)
        return new SearchParams() { Take = 5 };
      return new SearchParams() { OrderBy = paging.OrderBy, Skip = paging.Skip, Take = paging.Take };
    }
  }
}
