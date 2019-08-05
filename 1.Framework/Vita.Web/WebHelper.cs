using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {
  public static class WebHelper {

    public static bool IsSet(this WebOptions options, WebOptions option) {
      return (options & option) != 0;
    }

    public static WebCallContext GetWebCallContext(this HttpContext httpContext) {
      Util.Check(httpContext.Items.TryGetValue(WebCallContext.WebCallContextKey, out object webContextObj),
        "Failed to retrieve WebCallContext from request context, WebCallContextHandler middleware is not installed.");
      return (WebCallContext)webContextObj; 
    }
    public static void SetWebCallContext(this HttpContext httpContext, WebCallContext webContext) {
      httpContext.Items[WebCallContext.WebCallContextKey] = webContext; 
    }

    public static void SetupJwtTokenAuthentication(IServiceCollection services, string jwtSecret) {
      var jwtTokenHandler = new VitaJwtTokenHandler(EntityApp, services, jwtSecret);
      services.Add(new ServiceDescriptor(typeof(IAuthenticationTokenHandler), jwtTokenHandler));
    }

    public static void SetUserFromClaims(this OperationContext context, IEnumerable<Claim> claims) {
      Guid userId = Guid.Empty;
      string userName = string.Empty;
      long altUserId = 0;
      foreach (var claim in claims) {
        var v = claim.Value;
        switch (claim.Type) {
          case nameof(UserInfo.UserId):
            Guid.TryParse(claim.Value, out userId);
            break;
          case nameof(UserInfo.UserName):
            userName = claim.Value;
            break;
          case nameof(UserInfo.AltUserId):
            long.TryParse(claim.Value, out altUserId);
            break;
        } //switch
      } //foreach
      // Set UserInfo on current operation context
      context.User = new UserInfo(userId, userName, UserKind.AuthenticatedUser, altUserId);
    }



    // based on code from here: 
    // https://www.strathweb.com/2018/09/running-asp-net-core-content-negotiation-by-hand/
    public static Task WriteResponse <TModel>(this HttpContext context, TModel model) {
      var selector = context.RequestServices.GetRequiredService<OutputFormatterSelector>();
      var writerFactory = context.RequestServices.GetRequiredService<IHttpResponseStreamWriterFactory>();
      var formatterContext = new OutputFormatterWriteContext(context, writerFactory.CreateWriter, 
          typeof(TModel), model);
      var selectedFormatter = selector.SelectFormatter(formatterContext, Array.Empty<IOutputFormatter>(), new MediaTypeCollection());
      return selectedFormatter.WriteAsync(formatterContext);
    }

    public static void SetResponseHeader(this WebCallContext context, string name, string value) {
      context.EnsureResponseInfo();
      context.Response.Headers[name] = value; 
    }
    public static void SetResponse(this WebCallContext context, object body = null, string contentType = "text/plain",
                                      HttpStatusCode? status = null ) {
      context.EnsureResponseInfo(); 
      if (body != null) {
        context.Response.Body = body;
        context.Response.ContentType = contentType;
      }
      if (status != null)
        context.Response.HttpStatus = status.Value;
    }

    public static void EnsureResponseInfo(this WebCallContext context) {
      if (context.Response == null)
        context.Response = new ResponseInfo();
    }
  }
}
