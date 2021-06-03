using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Vita.Entities;

namespace BookStore.GraphQL {
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

    // copied from VitaJwtTokenHandler, it is used there for RESTful endpoints
    public static void SetUserFromClaims(this OperationContext context, IEnumerable<Claim> claims) {
      Guid userId = Guid.Empty;
      string userName = string.Empty;
      long altUserId = 0;
      foreach (var claim in claims) {
        var v = claim.Value;
        switch (claim.Type) {
          case nameof(UserInfo.UserId):
            Guid.TryParse(claim.Value, out userId);
            break;
          case nameof(UserInfo.UserName):
            userName = claim.Value;
            break;
          case nameof(UserInfo.AltUserId):
            long.TryParse(claim.Value, out altUserId);
            break;
        } //switch
      } //foreach
      // Set UserInfo on current operation context
      context.User = new UserInfo(userId, userName, UserKind.AuthenticatedUser, altUserId);
    }


  }
}
