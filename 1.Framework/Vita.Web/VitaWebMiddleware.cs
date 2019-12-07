using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Vita.Entities;
using Vita.Entities.Api;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;
using Vita.Entities.Services;


namespace Vita.Web {


  /// <summary>AspNetCore middleware 
  /// Creates and injects into HTTP context the WebCallContext (with OperationContext) holding all information about the web call. 
  /// Provides automatic web call logging and exception/error logging.  
  /// </summary>
  public class VitaWebMiddleware : IWebCallNotificationService {
    readonly RequestDelegate _next;
    public readonly EntityApp App;
    public readonly VitaWebMiddlewareSettings Settings;

    /*
    IWebCallLogService _webCallLog;
    IErrorLogService _errorLog;
    IUserSessionService _sessionService; 
    */


    public VitaWebMiddleware(RequestDelegate next, EntityApp app, VitaWebMiddlewareSettings settings) {
      _next = next;
      App = app;
      Settings = settings ?? new VitaWebMiddlewareSettings();
      App.RegisterConfig(Settings);
      /*
      _webCallLog = App.GetService<IWebCallLogService>();
      _errorLog = App.GetService<IErrorLogService>();
      _sessionService = App.GetService<IUserSessionService>();
      */
      App.RegisterService<IWebCallNotificationService>(this);

    }

    public async Task InvokeAsync(HttpContext context) {
      var webCtx = BeginRequest(context);
      Exception exc = null; 
      try {
        await _next(context);
      } catch (Exception ex) {
        exc = ex; 
      } finally {
        await EndRequestAsync(context, exc);
      }
    }

    public WebCallContext BeginRequest(HttpContext httpContext) {
      var req = httpContext.Request;
      var body = ReadRequestBodyForLog(req);
      var reqInfo = new RequestInfo() {
        ReceivedOn = AppTime.UtcNow,
        HttpMethod = req.Method,
        Url = req.GetDisplayUrl(),
        ContentType = req.ContentType,
        ContentSize = req.ContentLength,
        Body = body,
        Headers = req.Headers.ToDictionary(h => h.Key, h => string.Join(" ", h.Value)),
        IPAddress = httpContext.Connection.RemoteIpAddress.ToString(),
        HttpContextRef = new WeakReference(httpContext)
      };

      var log = new BufferedLog();
      var opCtx = new OperationContext(this.App,
        connectionMode: this.Settings.ConnectionReuseMode);
      opCtx.Log = new BufferedLog(opCtx.LogContext);
      var webContext = opCtx.WebContext = new WebCallContext(opCtx, reqInfo);

      httpContext.Items[WebCallContext.WebCallContextKey] = webContext;
      ReplaceResponseStream(httpContext, webContext);
      OnWebCallStarting(webContext);
      return webContext;
    }

    private async Task EndRequestAsync(HttpContext httpContext, Exception ex = null) {
      var webContext = httpContext.GetWebCallContext();
      if (webContext == null)
        return;
      try {
        RetrieveRoutingData(httpContext, webContext);
        if (ex != null) {
          webContext.Exception = ex;
          await EndFailedRequestAsync(httpContext, ex);
        } else if (webContext.Response != null) {
          SetExplicitResponse(webContext.Response, httpContext);
          return;
        }
        var resp = httpContext.Response;
        webContext.Response = new ResponseInfo();
        webContext.Response.Body = ReadResponseBodyForLog(resp);
      }catch(Exception) { 

      } finally {
        OnWebCallCompleting(webContext);
        webContext.OperationContext.DisposeAll(); // dispose/close conn
        RestoreOriginalResponseStream(httpContext, webContext);
      }
    }

    private async Task EndFailedRequestAsync(HttpContext httpContext, Exception ex) {
      var resp = httpContext.Response;
      var bodyWriter = new StreamWriter(resp.Body);
      switch (ex) {
        case ClientFaultException cfex:
          resp.StatusCode = (int)HttpStatusCode.BadRequest;
          await WriteResponseAsync(httpContext, cfex.Faults); // writes resp respecting content negotiation
          break;
        default:
          var bodyText = this.Settings.Options.IsSet(WebOptions.ReturnExceptionDetails) ?
                                     ex.ToLogString() : ex.Message;
          bodyWriter.Write(bodyText);
          resp.StatusCode = (int)HttpStatusCode.InternalServerError;
          break;
      }
      bodyWriter.Flush();
    }


    private void ReplaceResponseStream(HttpContext httpContext, WebCallContext webContext) {
      // Replace body stream with MemStream, so we can read response body for log
      // we will replace it back in EndRequest
      webContext.OriginalResponseStream = httpContext.Response.Body;
      httpContext.Response.Body = new MemoryStream();
    }

    private void RestoreOriginalResponseStream(HttpContext httpContext, WebCallContext webContext) {
      try {
        // Swap stream back to original, then copy data from temp stream into original
        var tempResponseStream = httpContext.Response.Body;
        var origResponseStream = webContext.OriginalResponseStream;
        httpContext.Response.Body = origResponseStream;
        webContext.OriginalResponseStream = null;
        // Now copy data
        // setting ContentLength is necessary to avoid 'too many bytes' stupid error thrown by Kestrel in some cases on CopyTo
        httpContext.Response.ContentLength = null;
        tempResponseStream.Position = 0;
        // actually copy all from our temp stream into real original stream
        tempResponseStream.CopyTo(origResponseStream);
        tempResponseStream.Flush();
      } catch (Exception ex) {
        // Keeping it to have a stop point in debugger, just in case
        Debug.WriteLine(ex.ToLogString());
        throw;
      }
    }

    private string ReadRequestBodyForLog(HttpRequest request) {
      request.EnableRewind();
      if (!IsTextContent(request.ContentType))
        return "(non-text content)";
      if (request.ContentLength == null || request.ContentLength == 0)
        return null;
      var body = request.Body;
      string textBody = null;
      // !!! Do not use 'using' or dispose reader - it will dispose the underlying stream
      var bodyReader = new StreamReader(body);
      textBody = bodyReader.ReadToEnd();
      body.Position = 0; //rewind it back
      return textBody;
    }

    private string ReadResponseBodyForLog(HttpResponse response) {
      if (!IsTextContent(response.ContentType))
        return "(non-text content)";
      response.Body.Position = 0;
      string textBody = null;
      var reader = new StreamReader(response.Body); 
      textBody = reader.ReadToEnd();
      //set explicit length, to avoid chunking
      if (response.ContentLength == null)
        response.ContentLength = textBody.Length; 
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
      webCtx.HandlerMethodName = action?.ToString();
      routeData.Values.TryGetValue("controller", out var contr);
      webCtx.HandlerControllerName = contr?.ToString();
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



    /*
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
          if(IgnoreUri(request.RequestUri))
            return await base.SendAsync(request, cancellationToken);
          WebCallInfo callInfo = null;
          WebCallContext webContext = null; 
          try {
            callInfo = new WebCallInfo(this.App, this.Settings, request, cancellationToken);
            webContext = callInfo.WebContext;
            PreprocessRequest(callInfo);
            OnWebCallStarting(callInfo); // Fire WebCallStarting event - UserSession service will handle it and attach user session and setup UserInfo in OperationContext
            if (callInfo.Response == null)
              callInfo.Response = await base.SendAsync(request, cancellationToken);
            OnWebCallEnding(callInfo);
            PostProcessResponse(callInfo);
            await LogWebCallInfo(callInfo);
            return callInfo.Response; 
          } catch(Exception ex) {
            LogError(ex, webContext);
            System.Diagnostics.Debug.WriteLine("Exception: " + ex.ToLogString());
            throw;
          }
        }//method

        private bool IgnoreUri(Uri uri) {
          var path = uri.LocalPath;
          if(Settings.FilterPaths.Count > 0) {
            if(!Settings.FilterPaths.Any(p => path.StartsWith(p)))
              return true; //ignore
          }
          if (Settings.IgnorePaths.Count > 0) {
            if(Settings.IgnorePaths.Any(p => path.StartsWith(p)))
              return true; 
          }
          return false;
        }

        private void PreprocessRequest(WebCallInfo callInfo) {
          var webCtx = callInfo.WebContext;
          // call token handlers (to handle headers and cookies)
          foreach (var handler in Settings.TokenHandlers)
            if (handler.Direction.IsSet(WebTokenDirection.Input))
              handler.HandleRequest(webCtx, callInfo.Request);
          if(_sessionService != null && !string.IsNullOrWhiteSpace(webCtx.UserSessionToken))
            _sessionService.AttachSession(webCtx.OperationContext, webCtx.UserSessionToken, webCtx.MinUserSessionVersion);
        }

        private void PostProcessResponse(WebCallInfo callInfo) {
          var webContext = callInfo.WebContext;
          try {
            //Make sure we dispose all disposables (open connections in sessions that reuse connection will be closed).
            webContext.OperationContext.DisposeAll();
            var request = callInfo.Request;
            var response = callInfo.Response;

            // Set explicit HTTP status code if requested
            if (webContext.OutgoingResponseStatus != null)
              response.StatusCode = webContext.OutgoingResponseStatus.Value;
            if (webContext.OutgoingResponseContent != null)
              response.Content = UnpackContent(webContext.OutgoingResponseContent);
            //Update/save session and get current session version
            var userSession = webContext.OperationContext.UserSession;
            if(_sessionService != null && userSession != null) {
              if(userSession.IsModified())
                _sessionService.UpdateSession(webContext.OperationContext);
              webContext.MinUserSessionVersion = userSession.Version; 
            }
            //call token handlers - to send back session version
            foreach(var handler in Settings.TokenHandlers)
              if (handler.Direction.IsSet(WebTokenDirection.Output))
                handler.HandleResponse(callInfo.WebContext, callInfo.Response);

            if (webContext.OutgoingCookies.Count > 0)
              response.SetCookies(webContext.OutgoingCookies, request.RequestUri.Host);
            if (webContext.OutgoingHeaders.Count > 0)
              WebHelper.SetHeaders(callInfo.Response, webContext);

            // Log critical errors first
            CheckResponseStatus(callInfo);
            CheckExceptions(callInfo);
          } catch(Exception ex) {
            webContext.Exception = ex;
            LogError(ex, webContext);
          } finally {
          }
        }

        private HttpContent UnpackContent(object outContent) {
          if(outContent == null)
            return null; 
          var httpCont = outContent as HttpContent;
          if(httpCont != null)
            return httpCont; 
          var text = outContent as string; 
          if (text != null) {
            var cont = new System.Net.Http.StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(text)));
            cont.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return cont; 
          }
          Util.Throw("Invalid OutgoingResponseContent value type: {0}. Must be string or HttpContent.", outContent.GetType());
          return null; 
        }

        private async Task LogWebCallInfo(WebCallInfo callInfo) {
          if(_webCallLog == null)
            return;
          var webContext = callInfo.WebContext;
          var opContext = webContext.OperationContext;
          var request = callInfo.Request;
          var response = callInfo.Response;
          // if no web call log then nothing else to do
          if(opContext.LogLevel == LogLevel.None)
            return;
          //Basic info
          webContext.TickCountEnd = App.TimeService.ElapsedMilliseconds;
          webContext.HttpStatus = response.StatusCode;
          switch(opContext.LogLevel) {
            case LogLevel.None: break;
            case LogLevel.Basic:
              break;
            case LogLevel.Details:
              //read request body and headers
              webContext.RequestHeaders = request.GetHeadersAsString(hideHeaders: Settings.NoLogHeaders);
              if(request.Content != null && !webContext.Flags.IsSet(WebCallFlags.HideRequestBody))
                webContext.RequestBody = await request.Content.SafeReadContent();
              //Note: for response some headers are missing compared to Fiddler log. 
              // The reason might be is that headers are completely formed later in the pipeline. 
              //TODO: investigate/improve response header log
              webContext.ResponseHeaders = response.GetHeadersAsString(Settings.NoLogHeaders);
              if (response.Content != null && !webContext.Flags.IsSet(WebCallFlags.HideResponseBody)) {
                webContext.ResponseBody = await response.Content.SafeReadContent();
              }
              break;
          } //switch logLevel
          if (webContext.ResponseBody != null)
            webContext.ResponseSize = webContext.ResponseBody.Length;
          // send to web log
          if(_webCallLog != null)
            _webCallLog.Log(webContext);
        }

        private Guid? LogError(Exception exception, WebCallContext webCallInfo) {
          if(_errorLog == null)
            return null;
          var id = _errorLog.LogError(exception, webCallInfo.OperationContext);
          return id;
        }

        private void CheckResponseStatus(WebCallInfo callInfo) {
          if(callInfo.Response.IsSuccessStatusCode)
            return; 
          // Note: maybe some more refined handling in the future (ex: bad requests, when too many we should do something)
          switch(callInfo.Response.StatusCode) {
            case HttpStatusCode.InternalServerError:
              //this a case when exception is not caught in exception filter - when it is thrown outside controller
              if (callInfo.WebContext.Exception == null)
                SafeReadErrorContent(callInfo);
              return; 
            default:
              return; 

          }
        }

        private async void CheckExceptions(WebCallInfo callInfo) {
          var exc = callInfo.WebContext.Exception;
          if(exc == null)
            return;
          if(exc is OperationAbortException)
            callInfo.Response = await ProcessOperationAbortException(callInfo);
          else
            callInfo.Response = ProcessServerErrorException(callInfo); 
        }

        private async Task<HttpResponseMessage> ProcessOperationAbortException(WebCallInfo callInfo) {
          var request = callInfo.Request;
          var webContext = callInfo.WebContext;
          var abortExc = webContext.Exception as OperationAbortException;
          if(abortExc.LogAsError) {
            webContext.OperationContext.LogLevel = LogLevel.Details;
            webContext.ErrorLogId = LogError(abortExc, webContext);
          }
          HttpResponseMessage errResp; 
          switch(abortExc.ReasonCode) {
            case OperationAbortReasons.ClientFault:
              var cfExc = (ClientFaultException) abortExc;
              errResp = new HttpResponseMessage(HttpStatusCode.BadRequest);
              var formatter = request.GetResponseFormatter(typeof(List<ClientFault>));
              errResp.Content = new ObjectContent(typeof(List<ClientFault>), cfExc.Faults, formatter);
              return errResp;
            case OperationAbortReasons.ConcurrencyViolation:
              errResp = new HttpResponseMessage(HttpStatusCode.Conflict);
              return errResp;
            case ClientFaultCodes.AuthenticationRequired:
              if(Settings.Options.IsSet(WebHandlerOptions.ReturnBadRequestOnAuthenticationRequired)) {
                errResp = new HttpResponseMessage(HttpStatusCode.BadRequest);
                var fault = new ClientFault() { Code = ClientFaultCodes.AuthenticationRequired, Message = "Authentication required." };
                var fmt = request.GetResponseFormatter(typeof(IList<ClientFault>));
                errResp.Content = new ObjectContent(typeof(IList<ClientFault>), new [] {fault}, fmt);
                return errResp; 
              } else {
                errResp = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                return errResp;
              }

            case ModelStateException.ReasonBadRequestBody:
              //Return BadRequest, and include detailed information about the deserializer failure
              errResp = new HttpResponseMessage(HttpStatusCode.BadRequest);
              var msExc = (ModelStateException) abortExc;
              var errors = msExc.ModelStateErrors;
              var reqContent =  await WebHelper.SafeReadContent(request.Content);
              var requestContentTrimmed = reqContent.TrimMiddle(512);
              var flt = new ClientFault() {Code = ClientFaultCodes.BadContent, Message = "Failure to deserialize body or parameters: " + errors };
              //Serialize it as json
              var errFmt = request.GetResponseFormatter(typeof(IList<ClientFault>));
              errResp.Content = new ObjectContent(typeof(IList<ClientFault>), new [] {flt}, errFmt);
              return errResp; 
            default:
              // Should never happen, currently other codes are not used. 
              errResp = new HttpResponseMessage(HttpStatusCode.BadRequest);
              errResp.Content = new StringContent(abortExc.ReasonCode);
              return errResp;
          }//switch
        }//method

        private HttpResponseMessage ProcessServerErrorException(WebCallInfo callInfo) {
          var response = callInfo.Response;
          var webContext = callInfo.WebContext;
          webContext.OperationContext.LogLevel = LogLevel.Details; //set detail log level so that everything is logged
          var exc = webContext.Exception;
          webContext.ErrorLogId = LogError(exc, callInfo.WebContext);
          var isAuthExc = (exc is Vita.Entities.Authorization.AuthorizationException);
          var returnStatus = isAuthExc ? HttpStatusCode.Forbidden : 
              (response.IsSuccessStatusCode ? HttpStatusCode.InternalServerError : response.StatusCode); // if it is already error status, keep it
          var errResponse = new HttpResponseMessage(returnStatus);
          // Return detailed exc info if flag is set
          if(this.Settings.Options.IsSet(WebHandlerOptions.ReturnExceptionDetails)) {
            var errDetails = exc.ToLogString();
            errResponse.Content = new StringContent(errDetails);
          } else
            errResponse.Content = new StringContent("Server error. See error log for details.");
          callInfo.Response = errResponse;
          return errResponse;
        }

        private void SafeReadErrorContent(WebCallInfo callInfo) {
          var errContent = callInfo.Response.Content as ObjectContent<HttpError>;
          if(errContent == null)
            return; 
          try {
            var httpErr = Task.Run(() => errContent.ReadAsAsync<HttpError>()).Result;
            var allAsString = string.Join(Environment.NewLine, httpErr.Select(kv => kv.Key + ":" + kv.Value));
            var exc = new Exception("Server error, not caught in ExceptionFilter.");
            exc.Data["OriginalException"] = allAsString; 
            callInfo.WebContext.Exception = exc; 
          } catch(Exception ex) {
            callInfo.WebContext.Exception = ex; 
          }

        }
        */


  } // WebCallContextHandler
}
