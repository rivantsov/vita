
using Microsoft.AspNetCore.Http;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {

  public static class WebExtensions {

    public static WebCallContext GetWebCallContext(this HttpContext httpContext) {
      Util.Check(httpContext.Items.TryGetValue(WebCallContext.WebCallContextKey, out object webContextObj),
        "Failed to retrieve WebCallContext from request context, WebCallContextHandler middleware is not installed.");
      return (WebCallContext)webContextObj; 
    }

  }
}
