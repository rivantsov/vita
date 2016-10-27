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

    /// <summary>A target for redirect callback from OAuth server, performed when the user authorizes the access on the target server authorization page.</summary>
    /// <param name="parameters">Redirect parameters.</param>
    /// <remarks>The method saves the information returned in parameters (Code, Error) in the OAuth flow record. 
    /// The State parameter contains the ID of the flow record representing the active OAuth process in the database.
    /// The Code parameter can be used immediately to retrieve the authorization token from the target server.</remarks>
    [ApiGet, ApiRoute("oauth_redirect")]
    // using [FromUrl] parameter to make routing match the method regardless of particular parameters present or missing
    public void OAuthRedirect([FromUrl] OAuthRedirectParams parameters) {
      var service = Context.App.GetService<IOAuthClientService>();
      service.OnRedirected(Context, parameters.State, parameters.Code, parameters.Error);
      var webctx = Context.WebContext;
      var stt = Context.App.GetConfig<OAuthClientSettings>();
      if (!string.IsNullOrEmpty(stt.RedirectResponseRedirectsTo)) {
        webctx.OutgoingResponseStatus = System.Net.HttpStatusCode.Redirect;
        webctx.OutgoingHeaders.Add("Location", stt.RedirectResponseRedirectsTo);
      } else {
        var msg = stt.RedirectResponseText ?? "Authorization completed.";
        webctx.OutgoingResponseContent = msg;
        webctx.OutgoingResponseStatus = System.Net.HttpStatusCode.OK; 
      }
    }//method

  } //class
}
