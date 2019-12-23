using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Utilities; 

namespace Vita.Modules.Logging {

  public class LogSaveService {
    LoggingEntityApp _app; 
    DoubleBufferCache<string, Guid> _userInfoIdCache;

    public LogSaveService(LoggingEntityApp app) {
      _app = app;
      _userInfoIdCache = new DoubleBufferCache<string, Guid>(); 
    }

    public void SaveEntries(IList<LogEntry> entries) {
      if (entries.Count == 0)
        return;
      var session = _app.OpenSystemSession();
      foreach (var entry in entries) {
        var iUserInfo = GetLogUserInfoEntity(session, entry.Context);
        switch (entry) {

          case InfoLogEntry infoEntry:
            var infoEnt = session.NewLogEntity<IOperationLog>(entry, iUserInfo);
            infoEnt.Message = infoEntry.AsText();
            break;

          case BatchedLogEntry batchEntry:
            var batchEnt = session.NewLogEntity<IOperationLog>(entry, iUserInfo);
            batchEnt.Message = batchEntry.AsText();
            break;

          case ErrorLogEntry errEntry:
            var errEnt = session.NewLogEntity<IErrorLog>(entry, iUserInfo);
            errEnt.Message = errEntry.Message;
            errEnt.Details = errEntry.Details;
            errEnt.ExceptionType = errEntry.ExceptionType?.Name;
            break;

          case WebCallLogEntry webEntry:
            var iwEnt = session.NewLogEntity<IWebCallLog>(entry, iUserInfo);
            var req = webEntry.Request; 
            iwEnt.Url = req.Url;
            iwEnt.UrlTemplate = req.UrlTemplate;
            iwEnt.HttpMethod = req.HttpMethod;
            iwEnt.IPAddress = req.IPAddress;
            iwEnt.RequestHeaders = HeadersToText(req.Headers);
            iwEnt.RequestBody = BodyAsText(req.Body);
            iwEnt.RequestSize = req.ContentSize; 
            var resp = webEntry.Response; 
            if (resp != null) {
              iwEnt.HttpStatus = resp.HttpStatus;
              iwEnt.ControllerName = resp.ControllerName;
              iwEnt.MethodName = resp.MethodName;
              iwEnt.ResponseHeaders = HeadersToText(resp.Headers);
              iwEnt.ResponseBody = BodyAsText(resp.Body);
              iwEnt.Duration = (int) resp.Duration.TotalMilliseconds; 
            }
            if (webEntry.Exception != null) {
              iwEnt.Error = webEntry.Exception.Message; 
              iwEnt.ErrorDetails = webEntry.Exception.ToLogString();
            }
            iwEnt.ErrorLogId = webEntry.ErrorLogId;
            break;
        }
      }


      session.SaveChanges();
    }

    private string BodyAsText(object body) {
      switch(body) {
        case null: return null;
        case string str: return StringHelper.TrimLength(str, 4096);
        case byte[] bytes:
        case Binary b: 
          return "(binary)";
        default:
          return null; 
      }
    }

    private string HeadersToText(IDictionary<string, string> headers) {
      var text = string.Join(" | ", headers.Select(de => $"{de.Key}={de.Value}"));
      return text; 
    }

    public ILogUserInfo GetLogUserInfoEntity (IEntitySession session, LogContext logContext) {
      var key = logContext.User.UserId + "/" + logContext.User.AltUserId;
      var userLogId = _userInfoIdCache.Lookup(key);
      if (userLogId != Guid.Empty)
        return session.GetEntity<ILogUserInfo>(userLogId, LoadFlags.Stub);
      var iUserInfo = session.NewLogUserInfo(logContext);
      _userInfoIdCache.Add(key, iUserInfo.Id);
      return iUserInfo; 
    }

  } //class
} //ns
