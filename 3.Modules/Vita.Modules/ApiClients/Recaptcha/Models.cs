using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.ApiClients.Recaptcha {

  //returned by google api
  public class RecaptchaResponse {
    public bool Success;
    public string[] Errors;
  }

  //Returned by RecaptchaApiController
  public class RecaptchaWidgetData {
    public string SiteKey;
    public string SecureToken;
  }

}
