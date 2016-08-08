using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

using Vita.Entities.Authorization.Runtime;

namespace Vita.Entities.Authorization {

  /// <summary>Authorization service. Provides methods for accessing user Authority object (total set of permissions) at runtime. </summary>
  public interface IAuthorizationService {
    
    /// <summary>Returns CachedAuthority object for a given user.</summary>
    /// <param name="user">UserInfo object.</param>
    /// <returns>AuthorityDescriptor instance.</returns>
    AuthorityDescriptor GetAuthority(UserInfo user);

    /// <summary>Returns Authority object for a given list of authorization roles. </summary>
    /// <param name="roles">A list of roles.</param>
    /// <returns>Authority instance.</returns>
    Authority GetAuthority(IList<Role> roles);

    /// <summary>Performs internal caches cleanup when user logs out. </summary>
    /// <param name="user">UserInfo object.</param>
    void UserLoggedOut(UserInfo user);
    
    /// <summary>Invalidates authority cache for a given user. Call this method after application changes the roles of a user. </summary>
    /// <param name="userId">User Id.</param>
    /// <param name="altUserId">Alternative integer user Id.</param>
    void InvalidateCachedAuthority(Guid userId, Int64 altUserId = 0);
  }


}
