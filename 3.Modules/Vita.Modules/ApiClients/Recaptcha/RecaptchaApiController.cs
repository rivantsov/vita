using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 
using Vita.Entities.Web;

namespace Vita.Modules.ApiClients.Recaptcha {

  [ApiRoutePrefix("recaptcha")]
  public class RecaptchaApiController : SlimApiController {

    [ApiGet, ApiRoute("widget")]
    public RecaptchaWidgetData GetWidget() {
      var recaptcha = Context.App.GetService<IRecaptchaService>();
      Util.Check(recaptcha != null, "IRecaptchaService not registered in EntityApp.");
      var data = new RecaptchaWidgetData() { SiteKey = recaptcha.GetSiteKey(), SecureToken = recaptcha.GetSecretToken() };
      return data; 
    }
    /* Sample Recaptcha widgets: 
       <div class="g-recaptcha" data-sitekey="(sitekey)"></div>   
     with secure token: 
       <div class="g-recaptcha" data-sitekey="(sitekey)" data-stoken="(securetoken)"></div>
     */


  }
}
