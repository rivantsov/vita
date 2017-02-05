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
using Vita.Entities.Services;
using Vita.Entities.Services.Implementations;

namespace Vita.Entities.Authorization {

  public class AuthorizationService: IAuthorizationService, IEntityService {
    public int RoleCacheExpirationSec = 60;
    public int UserCacheExpirationSec = 60;
    EntityApp _app;
    AuthorityBuilder _builder; 
    ObjectCache<string, Authority> _authorityByRoleSet;
    ObjectCache<string, AuthorityDescriptor> _authorityByUserId;

    public AuthorizationService(EntityApp app) {
      _app = app; 
      _builder = new AuthorityBuilder(_app); 
    }

    #region IEntityService Members

    public void Init(EntityApp app) {
      _app = app;
      _authorityByRoleSet = new ObjectCache<string, Authority>(
          expirationSeconds:  RoleCacheExpirationSec, maxLifeSeconds: RoleCacheExpirationSec * 5);
      _authorityByUserId = new ObjectCache<string, AuthorityDescriptor>( 
        expirationSeconds: UserCacheExpirationSec, maxLifeSeconds: UserCacheExpirationSec * 5);
      _authorityByUserId.Removed += AuthorityByUserId_Removed;  
    }

    private void AuthorityByUserId_Removed(object sender, ObjectCache<string, AuthorityDescriptor>.CacheItemRemovedEventArgs e) {
      var auth = e.Value;
      auth.Invalidated = true;

    }

    public void Shutdown() {
      
    }
    #endregion

    #region IAuthorizationService Members
    public Authority GetAuthority(IList<Role> userRoles) {
      var authorityKey = BuildRolesKey(userRoles);
      Authority authority = _authorityByRoleSet.Lookup(authorityKey); 
      if (authority != null)
        return authority; 
      //build new authority. 
      authority = _builder.BuildAuthority(authorityKey, userRoles);
      _authorityByRoleSet.Add(authority.Key, authority);
      return authority;
    }

    public AuthorityDescriptor GetAuthority(UserInfo user) {
      var userKey = user.Key; 
      var cAuth = _authorityByUserId.Lookup(userKey);
      if(cAuth == null) {
        var roles = _app.GetUserRoles(user);
        var auth = GetAuthority(roles);
        cAuth = new AuthorityDescriptor(auth); 
        _authorityByUserId.Add(userKey, cAuth);
      }
      return cAuth; 
    }

    public void UserLoggedOut(UserInfo user) {
      InvalidateCachedAuthority(user.UserId, user.AltUserId); 
    }

    public void InvalidateCachedAuthority(Guid userId, Int64 altUserId = 0) {
      // It is not enough to simply remove authority object from cache. Authority is also cached in UserInfo.Authority, and userInfo is saved in UserSession, 
      // so if there's an active user out there, he might still be using his saved userInfo.Authority object. We need to Invalidate this authority object. 
      // We do not need to do it here; we do it in on-remove handler - when item is removed from cache, it fires a callback in which we set auth.Invalidated = true; 
      // Secure session checks Invalidated flag, and if it is set, it refreshes the Authority object. 
      var key = UserInfo.GetKey(userId, altUserId);
      _authorityByUserId.Remove(key);
    }
    #endregion

    private static string BuildRolesKey(IList<Role> roles) {
      var sortedIds = roles.OrderBy(r => r.Id).Select(r => r.Id).ToList(); 
      return string.Join(",", sortedIds);
    }


  }//class
}
