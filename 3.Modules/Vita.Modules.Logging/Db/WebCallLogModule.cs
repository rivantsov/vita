using System;
using System.Collections.Generic;
using System.Linq; 
using System.Net;
using System.Text;
using Vita.Entities;
using Vita.Entities.Api;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging.Db {

  [Entity]
  public interface IWebCallLog {
    [PrimaryKey]
    Guid Id { get; set; }

    [Utc, Index]
    DateTime CreatedOn { get; set; }

    [Index]
    Guid UserId { get; set; }
    [Index]
    long? AltUserId { get; set; }

    [Nullable, Size(Sizes.UserName)]
    string UserName { get; set; }

    [Nullable, Unlimited]
    string Url { get; set; }

    [Nullable, Size(Sizes.IPv6Address)] //50
    string IPAddress { get; set; }
    [Size(10), Nullable]
    string HttpMethod { get; set; }
    [Nullable]
    string ControllerName { get; set; }
    [Nullable]
    string MethodName { get; set; }

    //Request
    [Nullable, Unlimited]
    string RequestHeaders { get; set; }
    [Nullable, Unlimited]
    string RequestBody { get; set; }
    long? RequestSize { get; set; }

    //Response
    HttpStatusCode HttpStatus { get; set; }
    [Nullable, Unlimited]
    string ResponseHeaders { get; set; }
    [Nullable, Unlimited]
    string ResponseBody { get; set; }
    long? ResponseSize { get; set; }
    int DurationMs { get; set; }


    //log and exceptions
    [Nullable, Unlimited]
    string LocalLog { get; set; }
    [Nullable, Unlimited]
    string Error { get; set; }
    [Nullable, Unlimited]
    string ErrorDetails { get; set; }

    Guid? ErrorLogId { get; set; }
  }

  public class WebCallLogModule : EntityModule, IObserver<LogEntryBatch> {
    public static readonly Version CurrentVersion = new Version("2.0.0.0");

    public WebCallLogModule(EntityArea area) : base(area, "WebCallLogModule", "Web call log.", CurrentVersion) {
      RegisterEntities(typeof(IWebCallLog));
    }

    public override void Init() {
      base.Init();
      var persService = this.App.GetService<ILogPersistenceService>();
      persService.Subscribe(this);
    }

    public void OnNext(LogEntryBatch batch) {
      if(!batch.EntriesByType.TryGetValue(typeof(WebCallLogEntry), out var appEventEntries))
        return;
      foreach(WebCallLogEntry entry in appEventEntries) {
        var ent = batch.Session.NewEntity<IWebCallLog>();
        ent.Id = entry.Id;
        ent.CreatedOn = entry.CreatedOn;
        var user = entry.Context.User;
        ent.UserId = user.UserId;
        ent.AltUserId = user.AltUserId;
        ent.UserName = user.UserName;

        var req = entry.Request; 
        ent.Url = req.Url;
        ent.IPAddress = req.IPAddress;
        ent.HttpMethod = req.HttpMethod;
        ent.RequestHeaders = DictAsString(req.Headers);
        ent.RequestBody = req.Body;
        ent.RequestSize = req.ContentSize;
        ent.ControllerName = req.HandlerControllerName;
        ent.MethodName = req.HandlerMethodName;

        var resp = entry.Response;
        ent.HttpStatus = resp.HttpStatus;
        ent.ResponseHeaders = DictAsString(resp.Headers);
        ent.ResponseSize = resp.Size;
        ent.DurationMs = resp.DurationMs;
        ent.LocalLog = resp.OperationLog;
        ent.Error = entry.Exception?.Message; 
        ent.ErrorDetails = entry.Exception?.ToLogString(); 
      }

    }

    private string DictAsString(IDictionary<string, string> dict) {
      return string.Join(Environment.NewLine, dict.Select(de => $"{de.Key}={de.Value}"));
    }

    public void OnCompleted() {
    }

    public void OnError(Exception error) {
    }

  }


}
