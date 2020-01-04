
using Microsoft.AspNetCore.Http;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {

  public static class VitaWebExtensions {

    public static WebCallContext GetWebCallContext(this HttpContext httpContext) {
      Util.Check(httpContext.Items.TryGetValue(WebCallContext.WebCallContextKey, out object webContextObj),
        "Failed to retrieve WebCallContext from HttpContext, VITA Web middleware is not activated in pipeline.");
      return (WebCallContext)webContextObj; 
    }

  }
}
