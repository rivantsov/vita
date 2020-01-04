using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Vita.Entities;
using Vita.Entities.Api;
using Vita.Entities.Logging;
using Vita.Entities.Services;


namespace Vita.Web {

  /// <summary>AspNetCore middleware 
  /// Creates and injects into HTTP context the WebCallContext (with OperationContext) holding all information about the web call. 
  /// Provides automatic web call logging and exception/error logging.  
  /// </summary>
  public class VitaWebMiddleware : IWebCallNotificationService {
    readonly RequestDelegate _next;
    public readonly EntityApp App;

    public VitaWebMiddleware(RequestDelegate next, EntityApp app) {
      _next = next;
      App = app;
      App.RegisterService<IWebCallNotificationService>(this);
    }

    public async Task InvokeAsync(HttpContext context) {
      var webCtx = await BeginRequestAsync(context);
      Exception exc = null; 
      try {
        await _next(context);
      } catch(ClientFaultException ex) {
        exc = ex;
      } catch (Exception ex) {
        exc = ex;
        throw; // rethrow
      } finally {
        await EndRequestAsync(context, exc);
      }
    }

    public async Task<WebCallContext> BeginRequestAsync(HttpContext httpContext) {
      var req = httpContext.Request;
      var reqInfo = new RequestInfo() {
        ReceivedOn = App.TimeService.UtcNow,
        StartTimestamp = Util.GetTimestamp(),
        HttpMethod = req.Method,
        Url = req.GetDisplayUrl(),
        ContentType = req.ContentType,
        ContentSize = req.ContentLength,
        Body = await ReadRequestBodyForLogAsync(req),
        Headers = req.Headers.ToDictionary(h => h.Key, h => string.Join(" ", h.Value)),
        IPAddress = httpContext.Connection.RemoteIpAddress.ToString(),
        HttpContextRef = new WeakReference(httpContext)
      };

      var log = new BufferedLog();
      var opCtx = new OperationContext(this.App, connectionMode: DbConnectionReuseMode.KeepOpen);
      var webContext = opCtx.WebContext = new WebCallContext(opCtx, reqInfo);
      opCtx.Log = new BufferedLog(opCtx.LogContext);

      httpContext.Items[WebCallContext.WebCallContextKey] = webContext;
      OnWebCallStarting(webContext);
      return webContext;
    }

    private async Task EndRequestAsync(HttpContext httpContext, Exception ex = null) {
      var webContext = httpContext.GetWebCallContext();
      if (webContext == null)
        return;
      try {
        // Clear request content if request is marked as confidential, for ex: login call with password inside
        //  the goal is to avoid writing passwords into logs
        if(webContext.Flags.IsSet(WebCallFlags.Confidential))
          webContext.Request.Body = "(confidential)";

        RetrieveRoutingData(httpContext, webContext);
        if (ex != null) {
          webContext.Request.Exception = ex;
          await EndFailedRequestAsync(httpContext, webContext.OperationContext, ex);
        } else if (webContext.Response != null) {
          SetExplicitResponse(webContext.Response, httpContext);
          return;
        }
        var resp = webContext.Response = new ResponseInfo();
        resp.HttpStatus = (HttpStatusCode) httpContext.Response.StatusCode;
        resp.BodyContentType = httpContext.Response.ContentType; 
        // Reading response stream does not work for now
        // webContext.Response.Body = await ReadResponseBodyForLogAsync(httpContext);
      }catch(Exception) { 

      } finally {
        webContext.Response.DurationMs = Util.GetTimeMsSince(webContext.Request.StartTimestamp);
        webContext.Response.OperationLog = webContext.OperationContext.GetLogContents();
        OnWebCallCompleting(webContext);
        webContext.OperationContext.DisposeAll(); // dispose/close conn
        App.LogService.AddEntry(new WebCallLogEntry(webContext));
      }
    }

    private async Task EndFailedRequestAsync(HttpContext httpContext, OperationContext opContext, Exception ex) {
      var resp = httpContext.Response;
      switch (ex) {
        case ClientFaultException cfex:
          resp.StatusCode = (int)HttpStatusCode.BadRequest;
          await WriteResponseAsync(httpContext, cfex.Faults); // writes resp respecting content negotiation
          break;
        default:
          App.LogService.LogError(ex, opContext.LogContext); 
          // the exception is already rethrown in the top Next method, we are just intercepting here to log it
          break;
      }
    }

    private async Task<string> ReadRequestBodyForLogAsync(HttpRequest request) {
      if (request.ContentLength == null || request.ContentLength == 0 || request.Body == null)
        return null;
      if (!IsTextContent(request.ContentType))
        return "(non-text content)";
      // !!! Do not use 'using' or dispose reader - it will dispose the underlying stream
      request.EnableBuffering(); 
      var bodyReader = new StreamReader(request.Body);
      var textBody = await bodyReader.ReadToEndAsync();
      request.Body.Position = 0; //rewind it back
      return textBody;
    }

    private static bool IsTextContent(string contentType) {
      if (string.IsNullOrWhiteSpace(contentType))
        return false;
      return contentType.Contains("text") || contentType.Contains("json");
    }

    private async void SetExplicitResponse(ResponseInfo wantedResponse, HttpContext httpContext) {
      httpContext.Response.StatusCode = (int)wantedResponse.HttpStatus; 
      if (wantedResponse.Body != null) {
        switch(wantedResponse.Body) {
          case string s:
            //httpContext.WriteResponse(s); 
            httpContext.Response.ContentType = wantedResponse.BodyContentType ?? "text/plain";
            httpContext.Response.Body = new MemoryStream(Encoding.UTF8.GetBytes(s));
            return;
          case byte[] bytes:
            await WriteResponseAsync(httpContext, bytes);
            //httpResponse.ContentType = wantedResponse.ContentType ?? "application/octet-stream";
            return; 
        }
      }
    }

    private void RetrieveRoutingData(HttpContext httpContext, WebCallContext webCtx) {
      var routeData = httpContext.GetRouteData();
      if (routeData == null)
        return; 
      routeData.Values.TryGetValue("action", out var action);
      webCtx.Request.HandlerMethodName = action?.ToString();
      routeData.Values.TryGetValue("controller", out var contr);
      webCtx.Request.HandlerControllerName = contr?.ToString();
    }

    #region IWebCallNotificationService implementation
    public event EventHandler<WebCallEventArgs> WebCallStarting;

    public event EventHandler<WebCallEventArgs> WebCallCompleting;

    // Fires WebCallStarting event - UserSessionService loads user session and attaches it to context, setting up currently logged in user
    private void OnWebCallStarting(WebCallContext webCtx) {
      WebCallStarting?.Invoke(this, new WebCallEventArgs(webCtx));
    }


    private void OnWebCallCompleting(WebCallContext webCtx) {
      WebCallCompleting?.Invoke(this, new WebCallEventArgs(webCtx));
    }
    #endregion

    // based on code from here: 
    // https://www.strathweb.com/2018/09/running-asp-net-core-content-negotiation-by-hand/
    private Task WriteResponseAsync<TModel>(HttpContext context, TModel model) {
      var selector = context.RequestServices.GetRequiredService<OutputFormatterSelector>();
      var writerFactory = context.RequestServices.GetRequiredService<IHttpResponseStreamWriterFactory>();
      var formatterContext = new OutputFormatterWriteContext(context, writerFactory.CreateWriter,
          typeof(TModel), model);
      var selectedFormatter = selector.SelectFormatter(formatterContext,
               Array.Empty<IOutputFormatter>(), new MediaTypeCollection());
      return selectedFormatter.WriteAsync(formatterContext);
    }

  } // class
}
