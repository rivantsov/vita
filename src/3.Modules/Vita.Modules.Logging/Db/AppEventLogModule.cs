using System;

using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging.Db {

  [Entity, Index("Category,EventType")]
  public interface IAppEvent {

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

    [Size(Sizes.Name)]
    string Category { get; set; }

    [Size(Sizes.Name)]
    string EventType { get; set; }

    EventSeverity Severity { get; set; }

    [Nullable, Unlimited]
    string Message { get; set; }

    Guid? SessionId { get; set; }
    Guid? WebCallId { get; set; }

    //Free-form values, 'main' value for easier search - rather than putting in parameters
    int? IntParam { get; set; }
    Guid? GuidParam { get; set; }
    [Nullable, Size(Sizes.Name)]
    string StringParam { get; set; }

  }


  public class AppEventLogModule: EntityModule, IObserver<LogEntryBatch> {
    public static readonly Version CurrentVersion = new Version("2.0.0.0");

    public AppEventLogModule(EntityArea area) : base(area, "AppEventLog", "App events log.", CurrentVersion) {
      RegisterEntities(typeof(IAppEvent));
    }

    public override void Init() {
      base.Init();
      var persService = this.App.GetService<ILogPersistenceService>();
      persService.Subscribe(this); 
    }

    public void OnNext(LogEntryBatch batch) {
      if (!batch.EntriesByType.TryGetValue(typeof(AppEventEntry), out var appEventEntries))
        return; 
      foreach(AppEventEntry appEvt in appEventEntries) {
        var iAppEvt = batch.Session.NewEntity<IAppEvent>();
        iAppEvt.Id = appEvt.Id; 
        iAppEvt.CreatedOn = appEvt.CreatedOn;
        var user = appEvt.Context.User; 
        iAppEvt.UserId = user.UserId;
        iAppEvt.AltUserId = user.AltUserId;
        iAppEvt.UserName = user.UserName;
        iAppEvt.Category = appEvt.Category;
        iAppEvt.EventType = appEvt.EventType;
        iAppEvt.Severity = appEvt.Severity;
        iAppEvt.Message = appEvt.Message;
        iAppEvt.StringParam = appEvt.StringParam;
        iAppEvt.IntParam = appEvt.IntParam;
        iAppEvt.GuidParam = appEvt.GuidParam;

        iAppEvt.SessionId = appEvt.Context.SessionId;
        iAppEvt.WebCallId = appEvt.Context.WebCallId;
      }

    }

    public void OnCompleted() {
    }

    public void OnError(Exception error) {
    }

  }
}
