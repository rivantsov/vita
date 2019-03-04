using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;

namespace Vita.Web {
  public static class WebHelper {

    public static bool IsSet(this WebHandlerOptions options, WebHandlerOptions option) {
      return (options & option) != 0;
    }

    public static bool IsSet(this WebTokenDirection flags, WebTokenDirection flag) {
      return (flags & flag) != 0;
    }

    public static void UseWebCallContextHandler(this IApplicationBuilder app, WebCallContextHandler handler) {
      app.UseMiddleware<WebCallContextMiddleware>(handler);

    }

  }
}
