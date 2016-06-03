using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Services;
using Vita.Entities.Web;
using Vita.Modules.OAuthClient.Internal;

namespace Vita.Modules.OAuthClient.Api {
  /// <summary>Serves as a direct target for OAuth redirect action.</summary>
  /// <remarks>You can configure your app in OAuth server to redirect back either to a page in your app,
  /// or to an API endpoint. In the latter case this controller provides such an end point.
  /// It is also convenient for unit tests. The controller fires a Redirect event in OAuthClientSettings.</remarks>
  public class OAuthRedirectController : SlimApiController {

    [ApiGet, ApiRoute("oauth_redirect")]
    // using [FromUrl] parameter to make routing match the method regardless of particular parameters present or missing
    public async Task OAuthRedirect([FromUrl] OAuthRedirectParams prms) {
      try {
        var webCtx = Context.WebContext;
        var uri = new Uri(webCtx.RequestUrl);
        // raise event 
        var stt = Context.App.GetConfig<OAuthClientSettings>();
        var service = Context.App.GetService<IOAuthClientService>();
        await service.OnRedirected(this.Context, prms);
        var redirectPath = uri.AbsolutePath; //without query
        var tokenInfo = await service.RetrieveAccessToken(this.Context, serverInfo, prms.Code, redirectPath);
      } catch(Exception ex) {
        System.Diagnostics.Debug.WriteLine("Ex: " + ex.ToLogString());
        throw; 
      }
    }

  }

}
