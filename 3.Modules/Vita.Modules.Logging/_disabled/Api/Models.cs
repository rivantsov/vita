using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Web;

namespace Vita.Modules.Logging.Api {


  public class UserSessionInfo {
    public Guid UserId;
    public string UserName;
    public DateTime Started;
    public DateTime? Expires;
    public int TimeOffsetMinutes;
  }

  public class LogData {
    public Guid Id;
    public DateTime CreatedOn;
    public string UserName;
    public Guid? UserSessionId;
    public Guid? WebCallId; 
  }

  public class ClientError {
    public Guid? Id; 
    public string Message;
    public string Details;
    public DateTime? LocalTime;
    public string AppName; 
  }

  public class ErrorData : LogData {
    public DateTime LocalTime;
    public string MachineName;
    public string AppName;
    public string Message;
    public string ExceptionType;
    public string Details;
    public string OperationLog;
    public bool IsClientError;
  }

  public class IncidentData : LogData {
    public string Type;
    public string SubType;
    public string Message;
    public Guid? KeyId1;
    public Guid? KeyId2;
    public string Key1;
    public string Key2;
    public string LongKey3;
    public string LongKey4;
    public string Notes;
  }

  public class IncidentAlertData : LogData {
    public string AlertType;
    public string IncidentType;
  }

  public class NotificationLogData : LogData {
    public string MediaType;
    public string Type;
    public string Recipients;
    public string MainRecipient; //main recipient
    public Guid? MainRecipeintUserId;
    public string Status;
    public string Error;
    public string Attachments;
  }

  public class LoginLogData : LogData {
    public Guid? LoginId;
    public string EventType; 
    public string Notes;
  }

  public class OperationLogData : LogData {
    public string Message;
  }

  public class TransactionLogData : LogData {
    public int Duration;
    public int RecordCount;
    //Contains list of refs in the form : EntityType/Operation/PK
    public string Changes;
  }

  public class WebCallLogData : LogData {

    public int Duration;
    public string Url;
    public string UrlReferrer;
    public string IPAddress;
    public string ControllerName;
    public string MethodName;

    //Request
    public string HttpMethod;
    public WebCallFlags Flags;
    public string CustomTags;
    public string RequestHeaders;
    public string RequestBody;
    public long? RequestSize;
    public int RequestObjectCount; //arbitrary, app-specific count of 'important' objects
    //Response
    public HttpStatusCode ResponseHttpStatus;
    public string ResponseHeaders;
    public string ResponseBody;
    public long? ResponseSize;
    public int ResponseObjectCount; //arbitrary, app-specific count of 'important' objects
    //log and exceptions
    public string LocalLog;
    public string Error;
    public string ErrorDetails;
    public Guid? ErrorLogId; 
  }

  public class EventData {
    public Guid Id;
    public string EventType;
    public DateTime? StartedOn;
    public int Duration;
    public string Location;
    public double? Value;
    public string StringValue; 
    public Guid? UserId;
    public Guid? TenantId;
    public Guid? SessionId;

    public IDictionary<string, string> Parameters;
  }

  /// <summary>A container for session token request. </summary>
  public class RefreshRequest {
    /// <summary>Refresh token returned by initial Login request. </summary>
    public string RefreshToken;
  }

  /// <summary>Response to refresh token request.</summary>
  public class RefreshResponse {
    /// <summary>A new session token (authentication token). </summary>
    public string NewSessionToken;
    /// <summary>A new refresh token, to be used in the future for new refresh request. </summary>
    public string NewRefreshToken;
  }


}//ns
