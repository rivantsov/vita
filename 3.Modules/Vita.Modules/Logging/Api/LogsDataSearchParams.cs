using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Logging.Api {

  public class ErrorLogQuery : SearchParams {
    public Guid? Id { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedUntil { get; set; }
    public string MachineName { get; set; }
    public string UserName { get; set; }
    public string AppName { get; set; }
    public string Keyword { get; set; } // find word in message
    public string ExceptionType { get; set; }
    public Guid? WebCallId { get; set; }
    public Guid? UserSessionId { get; set; }
    public bool? IsClientError { get; set; }
  }

  public class IncidentLogQuery : SearchParams {
    public Guid? Id { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedUntil { get; set; }
    public string Type { get; set; }
    public string SubType { get; set; }
    public string UserName { get; set; }
    public Guid? KeyId { get; set; }
    public string Keyword { get; set; }
  }

  public class WebCallLogQuery : SearchParams {
    public Guid? Id { get; set; }
    public DateTime? ReceivedFrom { get; set; }
    public DateTime? ReceivedUntil { get; set; }

    public string UserName { get; set; }
    public string HttpMethod { get; set; }
    public HttpStatusCode? HttpStatus { get; set; }
    public int? MinDuration { get; set; }

    public string Url { get; set; }
    public string IPAddress { get; set; }
    public string ControllerName { get; set; }
    public string MethodName { get; set; }
    //Request/response
    public long? MinRequestSize { get; set; }
    public long? MinResponseSize { get; set; }
    public string RequestHeadersContain { get; set; }
    public string ResponseHeadersContain { get; set; }
    //log and exceptions
    public bool ErrorsOnly { get; set; }
    public Guid? ErrorLogId { get; set; }
  }

  public class IncidentAlertQuery : SearchParams {
    public Guid? Id { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedUntil { get; set; }
    public string AlertType { get; set; }
    public string IncidentType { get; set; }
  }

  public class LoginLogQuery : SearchParams {
    public Guid? Id { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedUntil { get; set; }
    public string UserName { get; set; }
    public Guid? LoginId { get; set; }
    public string EventType { get; set; } 
    public Guid? WebCallId { get; set; }
    public string IpAddress { get; set; }
  }

  public class OperationLogQuery : SearchParams {
    public Guid? Id { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedUntil { get; set; }
    public Guid? UserId { get; set; }
    public Int64? AltUserId { get; set; }
    public string UserName { get; set; }
  }

  public class TransactionLogQuery : SearchParams {
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Int64? AltUserId { get; set; }
    public Guid? WebCallId { get; set; }
    public Guid? UserSessionId { get; set; }

    public DateTime StartedOn { get; set; }
    public int Duration { get; set; }
    public int RecordCount { get; set; }
    //Contains list of refs in the form : EntityType/Operation/PK
    public string Changes { get; set; }
  }

  public class EventLogQuery : SearchParams {
    public Guid? Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; }
    public DateTime? StartedFrom { get; set; }
    public DateTime? StartedUntil { get; set; }
    public int? MinDuration { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public string StringValuePrefix { get; set; }
    public string Location { get; set; }
  }

}//ns
