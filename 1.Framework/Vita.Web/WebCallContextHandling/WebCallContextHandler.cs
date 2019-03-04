using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Vita.Entities;
using Vita.Entities.Api;
using Vita.Entities.Runtime;
using Vita.Entities.Services;


namespace Vita.Web {

 
  /// <summary>AspNetCore middleware 
  /// Creates and injects into HTTP request object the WebCallContext, OperationContext holding all information about the web call. 
  /// Provides automatic web call logging and exception/error logging.  
  /// </summary>
  public class WebCallContextHandler: IWebCallNotificationService {
    public readonly EntityApp  App; 
    public readonly WebCallContextHandlerSettings Settings; 

    /*
    IWebCallLogService _webCallLog;
    IErrorLogService _errorLog;
    IUserSessionService _sessionService; 
    */

    public WebCallContextHandler(EntityApp app, WebCallContextHandlerSettings settings) {
      App = app;
      Settings = settings ?? new WebCallContextHandlerSettings();
      App.RegisterConfig(Settings);
      /*
      _webCallLog = App.GetService<IWebCallLogService>();
      _errorLog = App.GetService<IErrorLogService>();
      _sessionService = App.GetService<IUserSessionService>();
      */
      App.RegisterService<IWebCallNotificationService>(this); 
    }

    public Task BeginRequest(HttpContext httpContext) {
      var opCtx = new OperationContext(this.App);
      var webCtx = new WebCallContext(opCtx);
      httpContext.Items[WebCallContext.WebCallContextKey] = webCtx; 
      return Task.CompletedTask;
    }
    public Task EndRequest(HttpContext ctx, Exception ex = null) {
      return Task.CompletedTask; 
    }

    public WebCallContext CreateWebCallContext(HttpContext httpCtx, EntityApp app) {
      WebCallContext webCtx = null; // new WebCallContext(); 
      webCtx.OperationContext = new OperationContext(app, UserInfo.Anonymous, webCtx);
      //webCtx.OperationContext.SetExternalCancellationToken(cancellationToken);
      var request = httpCtx.Request;
      // webCtx.RequestUrl = httpCtx.Request. request.ur.RequestUri.ToString();
      webCtx.HttpMethod = request.Method.ToString().ToUpperInvariant();
      webCtx.RequestSize = request.Headers.ContentLength;
      return webCtx;
    }

    #region IWebCallNotificationService implementation
    public event EventHandler<WebCallEventArgs> WebCallStarting;

    public event EventHandler<WebCallEventArgs> WebCallCompleting;

    // Fires WebCallStarting event - UserSessionService loads user session and attaches it to context, setting up currently logged in user
    private void OnWebCallStarting(WebCallContext webCtx) {
      var evt = WebCallStarting;
      if (evt != null) {
        evt(this, new WebCallEventArgs(webCtx));
        // if(webCtx.ResponseMessage != null)
        //callInfo.Response = webCtx.ResponseMessage as HttpResponseMessage;
      }
    }


    private void OnWebCallEnding(WebCallContext webCtx) {
      var evt = WebCallCompleting;
      if (evt != null)
        evt(this, new WebCallEventArgs(webCtx));
    }
    #endregion



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
