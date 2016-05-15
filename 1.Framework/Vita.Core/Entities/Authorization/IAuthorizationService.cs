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

  public interface IAuthorizationService {
    Authority GetAuthority(UserInfo user);
    Authority GetAuthority(IList<Role> roles);
    void UserLoggedOut(UserInfo user);
  }


}
