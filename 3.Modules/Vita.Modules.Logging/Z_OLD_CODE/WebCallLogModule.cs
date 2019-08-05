using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Web;

namespace Vita.Modules.Logging {

  public class WebCallLogModule : EntityModule, IWebCallLogService, IObjectSaveHandler  {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    IBackgroundSaveService _backgroundSaveService;

    public WebCallLogModule(EntityArea area) : base(area, "WebLog", version: CurrentVersion) {
      RegisterEntities(typeof(IWebCallLog));
      App.RegisterService<IWebCallLogService>(this); 
    }

    public override void Init() {
      base.Init(); 
      _backgroundSaveService = App.GetService<IBackgroundSaveService>();
      _backgroundSaveService.RegisterObjectHandler(typeof(WebCallContext), this);
    }

    #region IWebCallLogService methods
    public void Log(WebCallContext webContext) {
      _backgroundSaveService.AddObject(webContext); 
    }
    #endregion

    #region IObjectSaveHandler interface members
    // called by background process to save the info in provided session
    public void SaveObjects(IEntitySession session, IList<object> items) {
      foreach (WebCallContext webCtx in items) {
        var ent = session.NewEntity<IWebCallLog>();
        ent.Id = webCtx.Id;
        ent.WebCallId = webCtx.Id; 
        ent.CreatedOn = webCtx.CreatedOn;
        ent.Duration = (int)(webCtx.TickCountEnd - webCtx.TickCountStart);
        ent.Url = webCtx.RequestUrl;
        ent.UrlTemplate = webCtx.RequestUrlTemplate;
        ent.UrlReferrer = webCtx.Referrer;
        ent.IPAddress = webCtx.IPAddress;
        var ctx = webCtx.OperationContext;
        if (ctx != null) {
          if (ctx.User != null)
            ent.UserName = ctx.User.UserName;
          if (ctx.LogLevel == LogLevel.Details)
            ent.LocalLog = ctx.GetLogContents();
          if (ctx.UserSession != null)
            ent.UserSessionId = ctx.UserSession.SessionId;
        }
        ent.ControllerName = webCtx.ControllerName;
        ent.MethodName = webCtx.MethodName;
        if (webCtx.Exception != null) {
          ent.Error = webCtx.Exception.Message;
          ent.ErrorDetails = webCtx.Exception.ToLogString();
        }
        ent.ErrorLogId = webCtx.ErrorLogId;
        ent.HttpMethod = webCtx.HttpMethod + string.Empty;
        ent.HttpStatus = webCtx.HttpStatus;
        ent.RequestSize = webCtx.RequestSize;
        ent.RequestHeaders = webCtx.RequestHeaders;
        ent.Flags = webCtx.Flags;
        if (webCtx.CustomTags.Count > 0)
          ent.CustomTags = string.Join(",", webCtx.CustomTags);
        if (webCtx.Flags.IsSet(WebCallFlags.HideRequestBody))
          ent.RequestBody = "(Omitted)";
        else
          ent.RequestBody = webCtx.RequestBody;

        ent.ResponseSize = webCtx.ResponseSize;
        ent.ResponseHeaders = webCtx.ResponseHeaders;
        if (webCtx.Flags.IsSet(WebCallFlags.HideResponseBody))
          ent.ResponseBody = "(Omitted)";
        else
          ent.ResponseBody = webCtx.ResponseBody;
        ent.RequestObjectCount = webCtx.RequestObjectCount;
        ent.ResponseObjectCount = webCtx.ResponseObjectCount;
      }
    }
    #endregion

  }

}
