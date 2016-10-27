using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;

namespace Vita.Modules.Logging.Api {

  [ApiRoutePrefix("logs"), LoggedInOnly, Secured]
  public class LoggingDataController : SlimApiController {

    protected IEntitySession OpenSession() {
      return Context.OpenSecureSession(); 
    }

    [ApiGet, ApiRoute("errors/{id}")]
    public ErrorData GetError(Guid id) {
      var session = OpenSession();
      var log = session.GetEntity<IErrorLog>(id);
      return log.ToModel(withDetails: true);
    }

    [ApiGet, ApiRoute("errors")]
    public SearchResults<ErrorData> GetErrors([FromUrl] ErrorLogQuery query) {
      var session = OpenSession();
      query = query.DefaultIfNull(50, defaultOrderBy: "CreatedOn:DESC");
      var where = session.NewPredicate<IErrorLog>()
           .AndIfNotEmpty(query.Id, e => e.Id == query.Id.Value)
           .AndIfNotEmpty(query.CreatedFrom, e => e.CreatedOn >= query.CreatedFrom.Value)
           .AndIfNotEmpty(query.CreatedUntil, e => e.CreatedOn <= query.CreatedUntil.Value)
           .AndIfNotEmpty(query.MachineName, e => e.MachineName.StartsWith(query.MachineName))
           .AndIfNotEmpty(query.UserName, e => e.UserName.StartsWith(query.UserName))
           .AndIfNotEmpty(query.AppName, e => e.AppName.StartsWith(query.AppName))
           .AndIfNotEmpty(query.Keyword, e => e.Message.Contains(query.Keyword)) // this can be sloooow
           .AndIfNotEmpty(query.ExceptionType, e => e.ExceptionType.StartsWith(query.ExceptionType))
           .AndIfNotEmpty(query.WebCallId, e => e.WebCallId == query.WebCallId.Value)
           .AndIfNotEmpty(query.UserSessionId, e => e.UserSessionId == query.UserSessionId.Value)
           .AndIfNotEmpty(query.IsClientError, e => e.IsClientError == query.IsClientError.Value);
      var results = session.ExecuteSearch(where, query, e => e.ToModel());
      return results; 
    }

    [ApiGet, ApiRoute("webcalls/{id}")]
    public WebCallLogData GetWebCall(Guid id) {
      var session = OpenSession();
      var log = session.GetEntity<IWebCallLog>(id);
      return log.ToModel(withDetails: true);
    }

    [ApiGet, ApiRoute("webcalls")]
    public SearchResults<WebCallLogData> GetWebCalls([FromUrl] WebCallLogQuery query) {
      var session = OpenSession();
      query = query.DefaultIfNull(50, defaultOrderBy: "ReceivedOn:DESC");
      var where = session.NewPredicate<IWebCallLog>()
           .AndIfNotEmpty(query.Id, wc => wc.Id == query.Id.Value)
           .AndIfNotEmpty(query.ReceivedFrom, wc => wc.CreatedOn >= query.ReceivedFrom.Value)
           .AndIfNotEmpty(query.ReceivedUntil, wc => wc.CreatedOn <= query.ReceivedUntil.Value)
           .AndIfNotEmpty(query.UserName, wc => wc.UserName.StartsWith(query.UserName))
           .AndIfNotEmpty(query.HttpMethod, wc => wc.HttpMethod == query.HttpMethod)
           .AndIfNotEmpty(query.HttpStatus, wc => wc.HttpStatus == query.HttpStatus.Value)
           .AndIfNotEmpty(query.MinDuration, wc => wc.Duration >= query.MinDuration.Value)
           .AndIfNotEmpty(query.Url, wc => wc.Url.StartsWith(query.Url))
           .AndIfNotEmpty(query.IPAddress, wc => wc.IPAddress.StartsWith(query.IPAddress))
           .AndIfNotEmpty(query.ControllerName, wc => wc.ControllerName.StartsWith(query.ControllerName))
           .AndIfNotEmpty(query.MethodName, wc => wc.MethodName.StartsWith(query.MethodName))
           .AndIfNotEmpty(query.MinRequestSize, wc => wc.RequestSize >= query.MinRequestSize.Value)
           .AndIfNotEmpty(query.MinResponseSize, wc => wc.ResponseSize >= query.MinResponseSize.Value)
           .AndIfNotEmpty(query.RequestHeadersContain, wc => wc.RequestHeaders.Contains(query.RequestHeadersContain))
           .AndIfNotEmpty(query.ResponseHeadersContain, wc => wc.ResponseHeaders.Contains(query.ResponseHeadersContain))
           .AndIf(query.ErrorsOnly, wc => wc.ErrorLogId != null)
           .AndIfNotEmpty(query.ErrorLogId, wc => wc.ErrorLogId == query.ErrorLogId.Value);
      var results = session.ExecuteSearch(where, query, wc => wc.ToModel());
      return results; 
    }

    [ApiGet, ApiRoute("incidents/{id}")]
    public IncidentData GetIncident(Guid id) {
      var session = OpenSession();
      var log = session.GetEntity<IIncidentLog>(id);
      return log.ToModel(withDetails: true);
    }

    [ApiGet, ApiRoute("incidents")]
    public SearchResults<IncidentData> GetIncidents([FromUrl] IncidentLogQuery query) {
      var session = OpenSession();
      query = query.DefaultIfNull(50, defaultOrderBy: "CreatedOn:DESC");
      var where = session.NewPredicate<IIncidentLog>()
           .AndIfNotEmpty(query.Id, inc => inc.Id == query.Id.Value)
           .AndIfNotEmpty(query.CreatedFrom, inc => inc.CreatedOn >= query.CreatedFrom.Value)
           .AndIfNotEmpty(query.CreatedUntil, inc => inc.CreatedOn <= query.CreatedUntil.Value)
           .AndIfNotEmpty(query.Type, inc => inc.Type.StartsWith(query.Type))
           .AndIfNotEmpty(query.SubType, inc => inc.SubType.StartsWith(query.SubType))
           .AndIfNotEmpty(query.UserName, inc => inc.UserName == query.UserName)
           .AndIfNotEmpty(query.Keyword, inc => inc.Message.Contains(query.Keyword)
                                                || inc.Key1.StartsWith(query.Keyword) || inc.Key2.StartsWith(query.Keyword))
           .AndIfNotEmpty(query.KeyId, inc => inc.KeyId1 == query.KeyId.Value || inc.KeyId2 == query.KeyId.Value);
      var results = session.ExecuteSearch(where, query, inc => inc.ToModel());
      return results; 
    }

    [ApiGet, ApiRoute("events/{id}")]
    public EventData GetEvent(Guid id) {
      var session = OpenSession();
      var evt = session.GetEntity<IEvent>(id);
      return evt.ToModel(details: true); 
    }

    [ApiGet, ApiRoute("events")]
    public SearchResults<EventData> SearchEvents([FromUrl] EventLogQuery query) {
      var session = OpenSession();
      query = query.DefaultIfNull(20, "StartedOn:desc");
      var where = session.NewPredicate<IEvent>()
        .AndIfNotEmpty(query.Id, e => e.Id == query.Id.Value)
        .AndIfNotEmpty(query.StartedFrom, e => e.StartedOn >= query.StartedFrom.Value)
        .AndIfNotEmpty(query.StartedUntil, e => e.StartedOn <= query.StartedUntil.Value)
        .AndIfNotEmpty(query.MinDuration, e => e.Duration >= query.MinDuration.Value)
        .AndIfNotEmpty(query.MinValue, e => e.Value >= query.MinValue.Value)
        .AndIfNotEmpty(query.MaxValue, e => e.Value <= query.MaxValue.Value)
        .AndIfNotEmpty(query.StringValuePrefix, e => e.StringValue.StartsWith(query.StringValuePrefix));
      if (!string.IsNullOrWhiteSpace(query.EventType)) {
        var hash = Util.StableHash(query.EventType);
        where = where.And(e => e.EventTypeHash == hash && e.EventType == query.EventType);
      }
      var results = session.ExecuteSearch(where, query, e => e.ToModel());
      return results; 

    }
  }//class

}
