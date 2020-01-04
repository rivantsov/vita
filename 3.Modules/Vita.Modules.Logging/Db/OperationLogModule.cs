using System;
using System.Collections.Generic;
using System.Text;

using Vita.Entities;
using Vita.Entities.Logging;

namespace Vita.Modules.Logging.Db {

  [Entity]
  public interface IOperationLog {

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
    string Message { get; set; }

    Guid? WebCallId { get; set; }

  }


  public class OperationLogModule: EntityModule, IObserver<LogEntryBatch> {
    public static readonly Version CurrentVersion = new Version("2.0.0.0");

    public OperationLogModule(EntityArea area) : base(area, "OperationLog", "Operation log.", CurrentVersion) {
      RegisterEntities(typeof(IOperationLog));
    }

    public override void Init() {
      base.Init();
      var persService = this.App.GetService<ILogPersistenceService>();
      persService.Subscribe(this); 
    }

    public void OnNext(LogEntryBatch batch) {
      if (!batch.EntriesByType.TryGetValue(typeof(InfoLogEntry), out var infoEntries))
        return; 
      foreach(LogEntry entry in infoEntries) {
        var iLogEnt = batch.Session.NewEntity<IOperationLog>();
        iLogEnt.Id = entry.Id; 
        iLogEnt.CreatedOn = entry.CreatedOn;
        var user = entry.Context.User; 
        iLogEnt.UserId = user.UserId;
        iLogEnt.AltUserId = user.AltUserId;
        iLogEnt.UserName = user.UserName;
        iLogEnt.Message = entry.AsText();
        iLogEnt.WebCallId = entry.Context.WebCallId;
      }

    }

    public void OnCompleted() {
    }

    public void OnError(Exception error) {
    }

  }
}
