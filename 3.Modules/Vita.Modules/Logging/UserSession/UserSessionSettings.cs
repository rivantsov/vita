using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Web;

namespace Vita.Modules.Logging {

  [Flags]
  public enum UserSessionOptions {
    None = 0,
    AutoSessionTimeout = 1,
    EnableLocalCache = 1 << 1, 
    Default = AutoSessionTimeout | EnableLocalCache,
  }


  public class UserSessionSettings {
    public UserSessionOptions Options;
    /// <summary>Used by default. </summary>
    public TimeSpan SessionTimeout = TimeSpan.FromMinutes(20);
    /// <summary>Used when client (typically mobile device) requests long sessions. </summary>
    public TimeSpan LongSessionTimeout = TimeSpan.FromDays(30);
    public int MemoryCacheExpirationSec;

    public Func<string> SessionTokenGenerator;

    public UserSessionSettings(UserSessionOptions options = UserSessionOptions.Default, int sessionTimeOutMinutes = 20, int longSessionTimeoutDays = 30,
                                     int memoryCacheExpirationSec = 2 * 60) {
      Options = options;
      SessionTimeout = TimeSpan.FromMinutes(sessionTimeOutMinutes);
      LongSessionTimeout = TimeSpan.FromDays(longSessionTimeoutDays);
      MemoryCacheExpirationSec = memoryCacheExpirationSec;
      SessionTokenGenerator = UserSessionModule.DefaultSessionTokenGenerator;
    }
  }
}
