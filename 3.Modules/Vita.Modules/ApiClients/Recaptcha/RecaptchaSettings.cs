using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.ApiClients.Recaptcha {

  [Flags]
  public enum RecaptchaOptions {
    None = 0,
    UseSecureToken = 1,
    CheckClientIp = 1 << 1,

    Default = None, 
  }

  public class RecaptchaSettings {
    public readonly string SiteKey;
    public readonly string SiteSecret;
    public readonly RecaptchaOptions Options; 

    public RecaptchaSettings(string siteKey, string siteSecret, RecaptchaOptions options = RecaptchaOptions.None) {
      SiteKey = siteKey;
      SiteSecret = siteSecret;
      Options = options; 
    }

  }//class

}//ns
