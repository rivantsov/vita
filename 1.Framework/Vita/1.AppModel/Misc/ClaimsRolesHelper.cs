using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Vita.Entities {
  public static class ClaimsRolesHelper {
    public const string UserRoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public static void AddRole(this IList<Claim> claims, string role) {
      claims.Add(new Claim(UserRoleClaimType, role));
    }
  }
}
