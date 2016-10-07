using System;

namespace Vita.Entities.Services {

  public enum UserSessionExpirationType {
    /// <summary>Short sliding session, expires after a period of inactivity.</summary>
    Sliding,        
    /// <summary>Session never expires. Typical for private secured computer. </summary>
    KeepLoggedIn,   
    /// <summary>Short-term fixed expiration (X minutes after login). Typical for public computer. </summary>
    FixedTerm,      
    /// <summary>Long term fixed expiration (3 months). Client can refresh the token and extend expiration date. 
    /// Typical for personal mobile device. </summary>
    LongFixedTerm,  

  }

  /// <summary>Service providing user session management. Implemented by user session module.  </summary>
  public interface IUserSessionService {
    UserSessionContext StartSession(OperationContext context, UserInfo user, UserSessionExpirationType expirationType = UserSessionExpirationType.Sliding);
    void AttachSession(OperationContext context, string sessionToken, long sessionVersion = 0, string csrfToken = null);
    void UpdateSession(OperationContext context);
    void EndSession(OperationContext context);
    string RefreshSessionToken(OperationContext context);
  }

}
