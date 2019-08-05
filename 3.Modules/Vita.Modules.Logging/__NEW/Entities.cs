using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Modules.Logging {

  [Entity, DoNotTrack]
  public interface IOperationLog : ILogEntityBase {
    [Unlimited]
    string Message { get; set; }
  }

  [DoNotTrack]
  public interface ILogContext {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    //Note - we do not use Auto(AutoType.CreatedOn) attribute here - if we did, it would result in datetime
    // of record creation, which happens later (on background thread) than actual event. 
    // So it should be set explicitly in each case, when the log call is made
    [Nullable, Size(Sizes.UserName)]
    //useful when there's no user session; also helps to have username directly in the table when looking 
    // at raw tables in SQL window
    string UserName { get; set; }
    [Index]
    Guid? SessionId { get; set; }
    Guid? TenantId { get; set; }

  }

  public interface ILogEntityBase {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    ILogContext LogContext { get; set; }
    [Utc, Index]
    DateTime CreatedOn { get; set; }

    [Index, Nullable]
    Guid? WebCallId { get; set; }
  }

  [Entity]
  public interface IAppEvent : ILogEntityBase {

    [Size(Sizes.Name)]
    string Category { get; set; }

    [Size(Sizes.Name)]
    string EventType { get; set; }
    [HashFor("EventType"), Index]
    int EventTypeHash { get; }

    [Size(Sizes.Name), Nullable]
    string EventSubType { get; set; }

    int? DurationMs { get; set; }

    //Free-form values, 'main' value for easier search - rather than putting in parameters
    int IntValue { get; set; }

    [Nullable, Unlimited]
    string Data { get; set; }

  }

  [Entity, OrderBy("CreatedOn:DESC"), ClusteredIndex("CreatedOn,Id"), DoNotTrack]
  public interface IErrorLog : ILogEntityBase {
    DateTime LocalTime { get; set; }

    [Size(50), Nullable]
    string MachineName { get; set; }

    [Size(50), Nullable, Index]
    string AppName { get; set; }
    [Size(250)]
    string Message { get; set; }
    [Size(100), Index]
    string ExceptionType { get; set; }
    [Unlimited, Nullable]
    string Details { get; set; }
    [Unlimited, Nullable]
    string OperationLog { get; set; }
    bool IsClientError { get; set; }

  }

  [Entity, DoNotTrack]
  public interface IWebCallLog : ILogEntityBase {
    int Duration { get; set; }

    [Nullable, Unlimited]
    string Url { get; set; }
    [Nullable, Size(250)]
    string UrlTemplate { get; set; }
    [Nullable, Size(250)]
    string UrlReferrer { get; set; }
    [Nullable, Size(Sizes.IPv6Address)] //50
    string IPAddress { get; set; }
    [Nullable]
    string ControllerName { get; set; }
    [Nullable]
    string MethodName { get; set; }

    //Request
    [Size(10), Nullable]
    string HttpMethod { get; set; }
    WebCallFlags Flags { get; set; }
    [Unlimited, Nullable]
    string CustomTags { get; set; }
    [Nullable, Unlimited]
    string RequestHeaders { get; set; }
    [Nullable, Unlimited]
    string RequestBody { get; set; }
    long? RequestSize { get; set; }
    int RequestObjectCount { get; set; } //arbitrary, app-specific count of 'important' objects

    //Response
    HttpStatusCode HttpStatus { get; set; }
    [Nullable, Unlimited]
    string ResponseHeaders { get; set; }
    [Nullable, Unlimited]
    string ResponseBody { get; set; }
    long? ResponseSize { get; set; }
    int ResponseObjectCount { get; set; } //arbitrary, app-specific count of 'important' objects

    //log and exceptions
    [Nullable, Unlimited]
    string LocalLog { get; set; }
    [Nullable, Unlimited]
    string Error { get; set; }
    [Nullable, Unlimited]
    string ErrorDetails { get; set; }

    Guid? ErrorLogId { get; set; }
  }

  [Entity, DoNotTrack]
  public interface IWebClientLog : ILogEntityBase {
    [Size(Sizes.Name), Nullable]
    string ClientName { get; set; }
    int Duration { get; set; }
    [Size(10)]
    string HttpMethod { get; set; }
    [Size(200)]
    string Server { get; set; } //protocol, domain address and port
    [Unlimited, Nullable]
    string PathQuery { get; set; }
    [Unlimited, Nullable]
    string CallTemplate { get; set; } //template used in a call, with placeholders like {0}
    [Nullable, Unlimited]
    string RequestHeaders { get; set; }
    [Unlimited, Nullable]
    string RequestBody { get; set; }
    long RequestSize { get; set; }

    //Response
    HttpStatusCode? ResponseHttpStatus { get; set; }
    [Nullable, Unlimited]
    string ResponseHeaders { get; set; }
    [Unlimited, Nullable]
    string ResponseBody { get; set; }
    long ResponseSize { get; set; }

    [Nullable, Unlimited]
    string Error { get; set; }
    Guid? ErrorLogId { get; set; }

    // hashes with indexes, used for fast search
    [HashFor("Server"), Index]
    int ServerHash { get; }
    [HashFor("CallTemplate"), Index]
    int CallTemplateHash { get; }

  }

  [Entity, DoNotTrack]
  public interface ITransactionLog : ILogEntityBase {

    int Duration { get; set; }
    int RecordCount { get; set; }

    [Index]
    long TransactonId { get; set; }

    [Nullable, Unlimited]
    string Comment { get; set; }

    [Nullable, Unlimited]
    //Contains list of refs in the form : EntityType/Operation/PK
    string Changes { get; set; }
  }


}
