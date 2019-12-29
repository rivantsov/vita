using System;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Utilities;

namespace Vita.Modules.Logging {

  public class ErrorLogModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    ILogService _logService; 

    public ErrorLogModule(EntityArea area) : base(area, "ErrorLog", "Error Logging Module.", version: CurrentVersion) {
      RegisterEntities(typeof(IErrorLog));
    }
    public override  void Init() {
      base.Init();
      _logService = App.GetService<ILogService>();
      _logService.Subscribe(LogService_EntryAdded);
    }

    // We listen to Log, catch all error entries, and write these to db immediately
    private void LogService_EntryAdded(LogEntry entry) {
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

    private void LogFatalLogFailure(Exception logExc, string originalError) {
      LogFatalLogFailure(logExc.ToLogString(), originalError);
    }

    private void LogFatalLogFailure(string logExc, string originalError) {
      LastResortErrorLog.Instance.LogFatalError(logExc, originalError);
    }

  }// ErrorLogModule class


}//ns
