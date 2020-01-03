using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;

namespace Vita.Modules.Logging {

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

  [Entity, DoNotTrack]
  public interface IUserSession : ILogEntityBase {

    [Index, Utc]
    DateTime StartedOn { get; set; }
    [Utc]
    DateTime? EndedOn { get; set; }
    [Utc]
    DateTime LastUsedOn { get; set; }

    UserSessionStatus Status { get; set; }

    //Expiration data
    UserSessionExpirationType ExpirationType { get; set; }
    DateTime? FixedExpiration { get; set; }
    int ExpirationWindowSeconds { get; set; }

    long Version { get; set; }

    int TimeZoneOffsetMinutes { get; set; }

    [Size(100), Nullable]
    string SessionToken { get; set; }
    DateTime TokenCreatedOn { get; set; }
    [Size(100), Nullable]
    string RefreshToken { get; set; }

    [Size(100), Nullable]
    string CsrfToken { get; set; }

    // Used for fast lookup by session token
    [HashFor(nameof(SessionToken)), Index]
    int TokenHash { get; set; }

    Guid? CreatedByWebCallId { get; set; }

    [Nullable, Size(50)] // IPV6
    string IPAddress { get; set; }

    [Nullable, Size(100)]
    string UserAgent { get; set; }
    [Nullable, Size(50)]
    string UserOS { get; set; }

    [Nullable, Unlimited]
    string Values { get; set; }
  }
}
