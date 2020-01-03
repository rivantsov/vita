using System;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging.Db {


  [Entity, OrderBy("CreatedOn:DESC"), ClusteredIndex("CreatedOn,Id")]
  public interface IErrorLog {
    [PrimaryKey]
    Guid Id { get; set; }

    //Note - we do not use Auto(AutoType.CreatedOn) attribute here - if we did, it would result in datetime
    // of record creation, which happens later (on background thread) than actual error. 
    // So it should be set explicitly in each case, when the log call is made
    [Utc, Index]
    DateTime CreatedOn { get; set; }

    [Size(Sizes.UserName)]
    string UserName { get; set; }

    [Index]
    Guid? UserId { get; set; }
    long? AltUserId { get; set; }

    Guid? UserSessionId { get; set; }

    Guid? WebCallId { get; set; }

    [Size(Sizes.Name), Nullable]
    string MachineName { get; set; }

    [Size(Sizes.Name), Nullable]
    string AppName { get; set; }

    ErrorKind Kind { get; set; }

    [Size(250)]
    string Message { get; set; }

    [Unlimited, Nullable]
    string Details { get; set; }

    [Unlimited, Nullable]
    string OperationLog { get; set; }

  }


  public class ErrorLogModule : EntityModule, IObserver<LogEntry> {
    public static readonly Version CurrentVersion = new Version("2.0.0.0");

    bool _autoSubscribeToLocalLog;

    public ErrorLogModule(EntityArea area, bool autoSubscribeToLocalLog = true) : base(area, "ErrorLog", "Error Logging Module.", version: CurrentVersion) {
      _autoSubscribeToLocalLog = autoSubscribeToLocalLog; 
      RegisterEntities(typeof(IErrorLog));
    }
    public override  void Init() {
      base.Init();
      if (_autoSubscribeToLocalLog) {
        var logService = App.GetService<ILogService>();
        logService.Subscribe(this);
      }
    }

    public void OnNext(LogEntry entry) {
      if (entry.IsError)
        LogError(entry); 
    }

    public void LogError(LogEntry entry) {
      var err = entry as ErrorLogEntry;
      if (err == null) { //something went very wrong, report it
        LogFatalLogFailure($"Invalid log entry, {entry.GetType()} : IsError returns true, but object is not ErrorLogEntry", 
             entry.ToLogString());
        return; 
      }
      var exc = err.Exception;
      if(!this.App.IsConnected()) {
        LogFatalLogFailure("(Log is not available, app not connected.)", exc.ToLogString());
        return;
      }
      var context = err.Context;
      if(err.Id == Guid.Empty)
        err.Id = Guid.NewGuid();
      try {
        var session = this.App.OpenSystemSession();
        var iErr = session.NewEntity<IErrorLog>();
        iErr.Id = err.Id;
        iErr.CreatedOn = err.CreatedOn;
        iErr.Message = Util.CheckLength(exc.Message, 250);
        iErr.Details = err.Details ?? exc.ToLogString(); 
        iErr.MachineName = Environment.MachineName;
        if(context != null) {
          iErr.AppName = this.App.AppName;
          //errInfo.OperationLog = context.GetLogContents();
          iErr.UserName = context.User.UserName;
          iErr.UserSessionId = context.SessionId;
          iErr.WebCallId = context.WebCallId;
          iErr.UserId = context.User.UserId;
          iErr.AltUserId = context.User.AltUserId;
        }
        session.SaveChanges();
      } catch (Exception logEx) {
        LogFatalLogFailure(logEx, exc.ToLogString());
      }
    }

    public void OnCompleted() {
    }

    public void OnError(Exception error) {
    }

    private void LogFatalLogFailure(Exception logExc, string originalError) {
      LogFatalLogFailure(logExc.ToLogString(), originalError);
    }

    private void LogFatalLogFailure(string logExc, string originalError) {
      LastResortErrorLog.Instance.LogFatalError(logExc, originalError);
    }

  }// ErrorLogModule class

}//ns
