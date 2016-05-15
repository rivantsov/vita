using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Authorization;

namespace Vita.Modules.Logging {

  public enum UserSessionExpirationType {
    Sliding,        // private computer, but auto-logout after inactivity for X minutes
    KeepLoggedIn,   // private trusted computer, stayed logged in for a long period (ex: 1 month); after this period request re-login
    FixedTerm,      // public computer, force expiration/relogin after short time

    Default = Sliding, 
  }

  public class UserSessionExpiration {
    public UserSessionExpirationType ExpirationType;
    public DateTime FixedExpiration;
    public TimeSpan SlidingWindow;

    public static UserSessionExpiration KeepLoggedIn = new UserSessionExpiration() { ExpirationType = UserSessionExpirationType.KeepLoggedIn };
  }


  /// <summary>Service providing user session management. Implemented by user session module.  </summary>
  public interface IUserSessionService {
    UserSessionContext StartSession(OperationContext context, UserInfo user, UserSessionExpiration expiration = null);
    void AttachSession(OperationContext context, string sessionToken, long sessionVersion = 0, string csrfToken = null);
    void UpdateSession(OperationContext context);
    void EndSession(OperationContext context);
  }

}
