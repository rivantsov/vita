using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {

  public static class WebExtensions {

    public static bool IsSet(this WebOptions options, WebOptions option) {
      return (options & option) != 0;
    }

    public static WebCallContext GetWebCallContext(this HttpContext httpContext) {
      Util.Check(httpContext.Items.TryGetValue(WebCallContext.WebCallContextKey, out object webContextObj),
        "Failed to retrieve WebCallContext from request context, WebCallContextHandler middleware is not installed.");
      return (WebCallContext)webContextObj; 
    }

  }
}
