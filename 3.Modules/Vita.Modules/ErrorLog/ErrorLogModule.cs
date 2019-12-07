using System;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Services;

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
      _logService.EntryAdded += LogService_EntryAdded;
    }

    private void LogService_EntryAdded(object sender, LogEntryEventArgs e) {
      if (e.Entry.EntryType != LogEntryType.Error)
        return;
      var errEntry = e.Entry as ErrorLogEntry;
      LogError(errEntry.Exception, errEntry.Context);
    }

    public Guid LogError(Exception exception, LogContext context = null) {
      if(!this.App.IsConnected()) {
        LogFatalLogFailure(new Exception("(Log is not available, app not connected.)"), exception.ToLogString());
        LastResortErrorLog.Instance.LogFatalError("(app not connected)", exception.ToLogString());
        return Guid.Empty;
      }
      try {
        var session = this.App.OpenSystemSession();
        var errInfo = session.NewEntity<IErrorLog>();
        errInfo.CreatedOn = App.TimeService.UtcNow;
        errInfo.Message = Util.CheckLength(exception.Message, 250);
        errInfo.Details = exception.ToLogString(); //writes exc.ToString() and exc.Data collection, along with all inner exception details
        errInfo.MachineName = Environment.MachineName;
        if(context != null) {
          errInfo.AppName = this.App.AppName;
          //errInfo.OperationLog = context.GetLogContents();
          errInfo.UserName = context.User.UserName;
          errInfo.UserSessionId = context.SessionId;
          errInfo.WebCallId = context.WebCallId; 
        }
        session.SaveChanges();
        return errInfo.Id;
      } catch (Exception logEx) {
        LastResortErrorLog.Instance.LogFatalError(logEx.ToLogString(), exception.ToLogString());
        return Guid.NewGuid(); 
      }
    }

    public Guid LogRemoteError(string message, string details, OperationContext context = null) {
      var errAll = message + Environment.NewLine + details;
      if (!this.App.IsConnected()) {
        LastResortErrorLog.Instance.LogFatalError("(app not connected)", errAll);
        return Guid.Empty; 
      }
      try {
        var session = this.App.OpenSystemSession();
        var errInfo = session.NewEntity<IErrorLog>();
        //Some messages might be really long; check length to fit into the field; full message will still go into details column
        errInfo.Message = Util.CheckLength(message, 250);
        errInfo.Details = details;
        errInfo.MachineName = Environment.MachineName;
        errInfo.CreatedOn = App.TimeService.UtcNow;
        if (context != null) {
          errInfo.AppName = context.App.AppName;
          errInfo.OperationLog = context.GetLogContents();
          errInfo.UserName = context.User.UserName;
          if (context.UserSession != null)
            errInfo.UserSessionId = context.UserSession.SessionId;
          if (context.WebContext != null)
            errInfo.WebCallId = context.WebContext.Id;
        }
        session.SaveChanges();
        return errInfo.Id;
      } catch (Exception logEx) {
        LogFatalLogFailure(logEx, errAll);
        return Guid.NewGuid();
      }
    }

    public Guid LogClientError(OperationContext context, Guid? id, string message, string details, string appName, DateTime? localTime = null) {
      var errAll = message + Environment.NewLine + details;
      try {
        var session = context.OpenSystemSession();
        IErrorLog errInfo;
        Guid idValue = id == null ? Guid.Empty : id.Value; 
        if (idValue != Guid.Empty) {
          //Check for duplicates
          errInfo = session.GetEntity<IErrorLog>(idValue); 
          if (errInfo != null)
            return idValue; 
        }
        errInfo = session.NewEntity<IErrorLog>();
        if(idValue != Guid.Empty)
          errInfo.Id = idValue; 
        //Some messages might be really long; check length to fit into the field; full message will still go into details column
        errInfo.Message = Util.CheckLength(message, 250);
        errInfo.Details = details;
        errInfo.MachineName = Environment.MachineName;
        errInfo.CreatedOn = App.TimeService.UtcNow;
        errInfo.AppName = appName ?? context.App.AppName;
        errInfo.OperationLog = context.GetLogContents();
        errInfo.UserName = context.User.UserName;
        if (context.UserSession != null)
          errInfo.UserSessionId = context.UserSession.SessionId;
        if (context.WebContext != null)
          errInfo.WebCallId = context.WebContext.Id;
        errInfo.IsClientError = true; 

        session.SaveChanges();
        return errInfo.Id;
      } catch(Exception logEx) {
        LogFatalLogFailure(logEx, errAll);
        return Guid.NewGuid();
      }
    }


    private void LogFatalLogFailure(Exception logExc, string originalError) {
      LastResortErrorLog.Instance.LogFatalError(logExc.ToLogString(), originalError);
    }

  }// ErrorLogModule class


}//ns
