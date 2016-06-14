using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.OAuthClient;

namespace Vita.Samples.OAuthDemoApp {

  public class OAuthEntityApp : EntityApp {
    public IOAuthClientService OAuthService; 

    public OAuthEntityApp(string serviceUrl) : base() {
      var data = AddArea("data");
      //This is URL of RedirectController - it will handle redirects from remote OAuth server
      var oauthStt = new OAuthClientSettings(redirectUrl: serviceUrl + "/api/oauth_redirect", 
        redirectResponseText: "Authorization completed, please return to OAuthDemoApp."); 
      OAuthService = new OAuthClientModule(data, oauthStt);
      var dbInfo = new Modules.DbInfo.DbInfoModule(data);
      var encrData = new Modules.EncryptedData.EncryptedDataModule(data);
      this.ApiConfiguration.RegisterController(typeof(Vita.Modules.OAuthClient.Api.OAuthRedirectController));
    }
  }
}
