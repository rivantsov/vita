using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;

namespace Vita.Modules.OAuthClient.Api {
  /// <summary>Serves as a direct target for OAuth redirect action.</summary>
  /// <remarks>You can configure your app in OAuth server to redirect back either to a page in your app,
  /// or to an API endpoint. In the latter case this controller provides such an end point.
  /// It is also convenient for unit tests. The controller fires a Redirect event in OAuthClientSettings.</remarks>
  public class OAuthRedirectController : SlimApiController {

    // Params passed with Redirect in URL; should be properties, not fields!  
    public class OAuthRedirectParams {
      public string Error { get; set; }  // not empty if error
      public string Code { get; set; }  //access code
      public string State { get; set; } //passed from AuthURL, flowId
    }

    [ApiGet, ApiRoute("oauth_redirect")]
    // using [FromUrl] parameter to make routing match the method regardless of particular parameters present or missing
    public void OAuthRedirect([FromUrl] OAuthRedirectParams prms) {
      var service = Context.App.GetService<IOAuthClientService>();
      service.OnRedirected(Context, prms.State, prms.Code, prms.Error);
      var webctx = Context.WebContext;
      var stt = Context.App.GetConfig<OAuthClientSettings>();
      if (!string.IsNullOrEmpty(stt.RedirectResponseRedirectsTo)) {
        webctx.OutgoingResponseStatus = System.Net.HttpStatusCode.Redirect;
        webctx.OutgoingHeaders.Add("Location", stt.RedirectResponseRedirectsTo);
      } else {
        var msg = stt.RedirectResponseText ?? "Authorization completed.";
        webctx.OutgoingResponseContent = new System.Net.Http.StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(msg)));
        webctx.OutgoingResponseContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        webctx.OutgoingResponseStatus = System.Net.HttpStatusCode.OK; 
      }
    }//method

  } //class
}
