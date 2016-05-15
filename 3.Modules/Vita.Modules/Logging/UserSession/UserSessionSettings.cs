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
    public TimeSpan SessionTimeout = TimeSpan.FromMinutes(20);
    public int MemoryCacheExpirationSec;

    public Func<string> SessionTokenGenerator;

    public UserSessionSettings(UserSessionOptions options = UserSessionOptions.Default, TimeSpan? sessionTimeOut = null,
                                     int memoryCacheExpirationSec = 2 * 60) {
      Options = options;
      if(sessionTimeOut != null)
        SessionTimeout = sessionTimeOut.Value;
      MemoryCacheExpirationSec = memoryCacheExpirationSec;
      SessionTokenGenerator = UserSessionModule.DefaultSessionTokenGenerator;
    }
  }
}
