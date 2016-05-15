using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.ApiClients.Recaptcha {

  public interface IRecaptchaService {
    string GetSiteKey();
    string GetSecretToken();
    bool Verify(string response, string clientIp = null, bool throwIfFail = true);
  }
}
