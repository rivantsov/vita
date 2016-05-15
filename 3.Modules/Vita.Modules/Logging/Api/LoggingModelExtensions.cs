using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Logging.Api {
  public static class LoggingModelExtensions {

    private static void AssignCommon(this LogData data, ILogEntityBase entity)    {
      data.Id = entity.Id;
      data.CreatedOn = entity.CreatedOn;
      data.UserName = entity.UserName;
      data.UserSessionId = entity.UserSessionId;
      data.WebCallId = entity.WebCallId;
    }

    public static ErrorData ToModel(this IErrorLog log, bool withDetails = false) {
      if(log == null)
        return null; 
      var data = new ErrorData() { AppName = log.AppName, LocalTime = log.LocalTime, MachineName = log.MachineName,
        ExceptionType = log.ExceptionType, Message = log.Message, 
        IsClientError = log.IsClientError
        // Details = log.Details, Message = log.Message, OperationLog = log.OperationLog
      };
      data.AssignCommon(log); 
      if(withDetails) {
        data.Details = log.Details; data.OperationLog = log.OperationLog;
      }
      return data; 
    }

    public static IncidentData ToModel(this IIncidentLog log, bool withDetails = false) {
      if(log == null)
        return null;
      var data = new IncidentData() {
        Type = log.Type, SubType = log.SubType, 
        Message = log.Message, 
        Key1 = log.Key1, Key2 = log.Key2, KeyId1 = log.KeyId1, KeyId2 = log.KeyId2,
        LongKey3 = log.LongKey3, LongKey4 = log.LongKey4         
      };
      data.AssignCommon(log);
      if (withDetails) {
        data.Notes = log.Notes; 
      }
      return data;
    }
    public static IncidentAlertData ToModel(this IIncidentAlert alert, bool withDetails = false) {
      if(alert == null)
        return null;
      var data = new IncidentAlertData() {AlertType = alert.AlertType, IncidentType = alert.IncidentType  };
      if(withDetails) {
      }
      data.AssignCommon(alert);
      return data;
    }

    public static NotificationLogData ToModel(this INotificationLog log, bool withDetails = false) {
      if(log == null)
        return null;
      var data = new NotificationLogData() {
        Type = log.Type, MediaType = log.MediaType,
        Recipients = log.Recipients, MainRecipient = log.MainRecipient,  MainRecipeintUserId = log.MainRecipientUserId,
        Status = log.Status.ToString(), Error = log.Error, Attachments = log.AttachmentList, 
      };
      data.AssignCommon(log); 
      return data;
    }

    public static LoginLogData ToModel(this ILoginLog log, bool withDetails = false) {
      if(log == null)
        return null;
      var data = new LoginLogData() {  EventType = log.EventType,   LoginId = log.LoginId  };
      if(withDetails) {
        data.Notes = log.Notes; 
      }
      data.AssignCommon(log);
      return data;
    }
    
    public static OperationLogData ToModel(this IOperationLog log, bool withDetails = false) {
      if(log == null)
        return null;
      var data = new OperationLogData() {Id = log.Id, CreatedOn = log.CreatedOn, UserName = log.UserName, 
        WebCallId = log.WebCallId, UserSessionId = log.UserSessionId,  Message = log.Message };
      if(withDetails) {
      }
      data.AssignCommon(log); 
      return data;
    }

    public static TransactionLogData ToModel(this ITransactionLog log, bool withDetails = false) {
      if(log == null)
        return null;
      var data = new TransactionLogData() { Duration = log.Duration, RecordCount = log.RecordCount };
      if(withDetails) {
        data.Changes = log.Changes;
      }
      data.AssignCommon(log); 
      return data;
    }
    public static WebCallLogData ToModel(this IWebCallLog log, bool withDetails = false) {
      if(log == null)
        return null;
      var data = new WebCallLogData() { 
        Duration = log.Duration,
        Url = log.Url, IPAddress = log.IPAddress, Flags = log.Flags, CustomTags = log.CustomTags, 
        UrlReferrer = log.UrlReferrer, HttpMethod = log.HttpMethod,
        ControllerName = log.ControllerName, MethodName = log.MethodName,
        ResponseSize = log.ResponseSize, ResponseHttpStatus = log.HttpStatus, ResponseObjectCount = log.ResponseObjectCount,
        RequestSize = log.RequestSize, RequestObjectCount = log.RequestObjectCount,
        ErrorLogId = log.ErrorLogId, Error = log.Error, 
      };
      if(withDetails) {
          data.RequestHeaders = log.RequestHeaders; data.RequestBody = log.RequestBody; 
          data.ResponseHeaders = log.RequestHeaders; data.ResponseBody = log.ResponseBody;
          data.LocalLog = log.LocalLog; data.ErrorDetails = log.ErrorDetails;
      }
      data.AssignCommon(log);
      return data;
    }

    public static EventData ToModel(this IEvent evt, bool details = false) {
      var model = new EventData() {
        Id = evt.Id, EventType = evt.EventType, StartedOn = evt.StartedOn, Duration = evt.Duration,
        UserId = evt.UserId, SessionId = evt.SessionId, TenantId = evt.TenantId, Location = evt.Location,
        Value = evt.Value, StringValue = evt.StringValue
      };
      if (details)
        model.Parameters = evt.Parameters.ToDictionary(e => e.Name, e => e.Value);
      return model; 
    }

  }//class
}
