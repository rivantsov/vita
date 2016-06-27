using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.OAuthClient;

namespace Vita.Samples.OAuthDemoApp {

  public class OAuthEntityApp : EntityApp {

    public OAuthEntityApp(string serviceUrl) : base() {
      var data = AddArea("data");
      // Note: we have to specify redirect URL explicitly here; for real world apps the redirect URL may be left null -
      //  it will be configured automatically by OAuthClientModule on first web api call 
      // (see EntityApp.WebInitialize, OAuthClientModule.WebInitialize). But in this demo app the initialization will 
      // happen only when the controller is hit (by redirect) - but we need to know redirect URL before that. 
      // This is URL of RedirectController - it will handle redirects from remote OAuth server
      var oauthStt = new OAuthClientSettings(redirectUrl: serviceUrl + "/api/oauth_redirect", 
        defaultAccountName: "TestAccount",
        redirectResponseText: "Authorization completed, please return to OAuthDemoApp."); 
      //Note for redirectResponseText: alternatively you can specify after-redirect landing page (another redirect from RedirectController)
      var oauth = new OAuthClientModule(data, oauthStt);
      var dbInfo = new Modules.DbInfo.DbInfoModule(data);
      var encrData = new Modules.EncryptedData.EncryptedDataModule(data);
      this.ApiConfiguration.RegisterController(typeof(Vita.Modules.OAuthClient.Api.OAuthRedirectController));
    }
  }
}
