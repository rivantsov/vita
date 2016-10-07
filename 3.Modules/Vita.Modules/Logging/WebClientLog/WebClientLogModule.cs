using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.WebClient;

namespace Vita.Modules.Logging {

  public class WebClientLogModule : EntityModule, IWebClientLogService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    IBackgroundSaveService _saveService; 

    public WebClientLogModule(EntityArea area) : base(area, "WebClientLog", version: CurrentVersion) {
      this.RegisterEntities(typeof(IWebClientLog));
      App.RegisterService<IWebClientLogService>(this); 
    }

    public override void Init() {
      base.Init();
      _saveService = App.GetService<IBackgroundSaveService>(); 
    }

    #region IWebClientLogService

    class ClientLogEntry : IObjectSaveHandler {
      public string ClientName;
      public DateTime CreatedOn; 
      public string UserName;
      public Guid UserSessionId; 
      public int Duration;
      public Uri RequestUri;
      public string HttpMethod;
      public HttpStatusCode? HttpStatus;
      public string CallTemplate;
      public string RequestHeaders;
      public string RequestBody; 
      public string ResponseHeaders;
      public string ResponseBody; 
      public Exception Exception;
      public Guid? ErrorLogId;
      public Guid? WebCallId; 

      public void SaveObjects(IEntitySession session, IList<object> items) {
        foreach(ClientLogEntry entry in items) {
          var ent = session.NewEntity<IWebClientLog>();
          ent.ClientName = entry.ClientName;
          ent.CreatedOn = entry.CreatedOn;
          ent.UserName = entry.UserName;
          ent.UserSessionId = entry.UserSessionId;
          ent.WebCallId = entry.WebCallId;
          ent.Duration = entry.Duration;
          ent.HttpMethod = entry.HttpMethod;
          ent.ResponseHttpStatus = entry.HttpStatus;
          if(entry.RequestUri != null) {
            ent.Server = entry.RequestUri.Scheme + "://" + entry.RequestUri.Authority;
            ent.PathQuery = entry.RequestUri.PathAndQuery;
          } else
            ent.Server = "(unknown)"; //just in case
          ent.CallTemplate = entry.CallTemplate;
          try {
            ent.RequestHeaders = entry.RequestHeaders;
            ent.RequestBody = entry.RequestBody;
            ent.ResponseHeaders = entry.ResponseHeaders;
            ent.ResponseBody = entry.ResponseBody;
            if(ent.RequestBody != null)
              ent.RequestSize = ent.RequestBody.Length;
            if(ent.ResponseBody != null)
              ent.ResponseSize = ent.ResponseBody.Length;
            if(entry.Exception != null)
              ent.Error = entry.Exception.ToLogString();
            ent.ErrorLogId = entry.ErrorLogId;
          } catch (Exception ex) {
            ent.Error = "ERROR saving client log: " + ex.ToLogString(); 
          }
        }//foreach
      }//method
    }//class

    public void Log(OperationContext context, string clientName, long duration, string urlTemplate, object[] urlArgs,
         HttpRequestMessage request, HttpResponseMessage response, 
         SerializedContent requestContent, SerializedContent responseContent,
         Exception exception) {
      try {
        var entry = new ClientLogEntry() {
          ClientName = clientName, 
          CreatedOn = context.App.TimeService.UtcNow, UserName = context.User.UserName, Duration = (int)duration, CallTemplate = urlTemplate,
          RequestUri = request?.RequestUri, HttpMethod = request?.Method.Method, RequestHeaders = request?.GetHeadersAsString(), RequestBody = requestContent?.Raw,
          ResponseHeaders = response?.GetHeadersAsString(), HttpStatus = response?.StatusCode, ResponseBody = responseContent?.Raw,
          Exception = exception
        };
        if(context.UserSession != null)
          entry.UserSessionId = context.UserSession.SessionId;
        if(context.WebContext != null) {
          entry.WebCallId = context.WebContext.Id;
          if(exception != null && context.WebContext.ErrorLogId == null)
            context.WebContext.ErrorLogId = Guid.NewGuid();
          entry.ErrorLogId = context.WebContext.ErrorLogId;
        }
        _saveService.AddObject(entry);
      } catch (Exception exc) {
        var excList = new List<Exception>();
        excList.Add(exc);
        if(exception != null)
          excList.Add(exception);
        throw new AggregateException("Error in web client log: " + exc.Message, excList);
      }
    }
    #endregion

  } //class
} //ns 
