using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Vita.Web {

  public class WebCallContextMiddleware {

    readonly RequestDelegate _next;
    WebCallContextHandler _handler;
    

    public WebCallContextMiddleware(RequestDelegate next, WebCallContextHandler handler) {
      _next = next;
      _handler = handler; 
    }

    public async Task InvokeAsync(HttpContext context) {
      await _handler.BeginRequest(context);
      try {
        await _next(context);
        await _handler.EndRequest(context);
      } catch (Exception ex) {
        await _handler.EndRequest(context, ex);
      }
    }

  }//class
}
