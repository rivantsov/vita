using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {
  public static class WebHelper {

    public static bool IsSet(this WebHandlerOptions options, WebHandlerOptions option) {
      return (options & option) != 0;
    }

    public static bool IsSet(this WebTokenDirection flags, WebTokenDirection flag) {
      return (flags & flag) != 0;
    }

    public static WebCallContext GetWebCallContext(this HttpContext httpContext) {
      Util.Check(httpContext.Items.TryGetValue(WebCallContext.WebCallContextKey, out object webContextObj),
        "Failed to retrieve WebCallContext from request context, WebCallContextHandler middleware is not installed.");
      return (WebCallContext)webContextObj; 
    }
    public static void SetWebCallContext(HttpContext httpContext, WebCallContext webContext) {
      httpContext.Items[WebCallContext.WebCallContextKey] = webContext; 
    }

  }
}
