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
    public int RoleCacheExpirationSec = 300;
    public int UserCacheExpirationSec = 60;
    EntityApp _app;
    AuthorityBuilder _builder; 
    ObjectCache<Authority> _authorityByRoleSet;
    ObjectCache<Authority> _authorityByUserId;

    public AuthorizationService(EntityApp app) {
      _app = app; 
      _app.RegisterService<IAuthorizationService>(this);
      _builder = new AuthorityBuilder(_app); 
    }

    #region IEntityService Members

    public void Init(EntityApp app) {
      _app = app;
      _authorityByRoleSet = new ObjectCache<Authority>("AuthorityByRoleSet", RoleCacheExpirationSec);
      _authorityByUserId = new ObjectCache<Authority>("AuthorityByUserId", UserCacheExpirationSec);
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
    public Authority GetAuthority(UserInfo user) {
      var userKey = user.Key; 
      var auth = _authorityByUserId.Lookup(userKey);
      if(auth == null) {
        var roles = _app.GetUserRoles(user);
        auth = GetAuthority(roles);
        _authorityByUserId.Add(userKey, auth);
      }
      user.Authority = auth;
      return auth; 
    }

    public void UserLoggedOut(UserInfo user) {
      _authorityByUserId.Remove(user.Key);
    }
    #endregion

    private static string BuildRolesKey(IList<Role> roles) {
      var sortedIds = roles.OrderBy(r => r.Id).Select(r => r.Id).ToList(); 
      return string.Join(",", sortedIds);
    }


  }//class
}
