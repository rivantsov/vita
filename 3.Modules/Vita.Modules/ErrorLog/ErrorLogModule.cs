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
      LogError(errEntry.Exception, errEntry.Context) ??? ;
    }

    #region IErrorLogService Members

    public Guid LogError(Exception exception, OperationContext context = null) {
      if(!this.App.IsConnected()) {
        LastResortErrorLog.Instance.LogFatalError("(app not connected)", exception.ToLogString());
        OnErrorLogged(context, exception);
        return Guid.Empty;
      }
      try {
        var session = this.App.OpenSystemSession();
        var errInfo = session.NewEntity<IErrorLog>();
        errInfo.CreatedOn = App.TimeService.UtcNow;
        errInfo.Message = Util.CheckLength(exception.Message, 250);
        errInfo.Details = exception.ToLogString(); //writes exc.ToString() and exc.Data collection, along with all inner exception details
        errInfo.ExceptionType = exception.GetType().Name;
        errInfo.MachineName = Environment.MachineName;
        if(context != null) {
          errInfo.AppName = context.App.AppName;
          errInfo.OperationLog = context.GetLogContents();
          errInfo.UserName = context.User.UserName;
          if(context.UserSession != null)
            errInfo.UserSessionId = context.UserSession.SessionId;
          if (context.WebContext != null)
            errInfo.WebCallId = context.WebContext.Request.Id; 
        }
        session.SaveChanges();
        OnErrorLogged(context, exception);
        return errInfo.Id;
      } catch (Exception logEx) {
        LastResortErrorLog.Instance.LogFatalError(logEx.ToLogString(), exception.ToLogString());
        return Guid.NewGuid(); 
      }
    }

    public Guid LogRemoteError(string message, string details, OperationContext context = null) {
      var errAll = message + Environment.NewLine + details;
      if (!this.App.IsConnected()) {
        OnErrorLogged(context, new Exception(errAll));
        LastResortErrorLog.Instance.LogFatalError("(app not connected)", errAll);
        return Guid.Empty; 
      }
      try {
        var session = this.App.OpenSystemSession();
        var errInfo = session.NewEntity<IErrorLog>();
        errInfo.ExceptionType = "Unknown";
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
        OnErrorLogged(context, new Exception(message + Environment.NewLine + details));
        return errInfo.Id;
      } catch (Exception logEx) {
        WriteToSystemLog(logEx, message, details);
        LastResortErrorLog.Instance.LogFatalError(logEx.ToLogString(), errAll);
        return Guid.NewGuid();
      }
    }

    public Guid LogClientError(OperationContext context, Guid? id, string message, string details, string appName, DateTime? localTime = null) {
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
        errInfo.ExceptionType = "ClientError";
        //Some messages might be really long; check length to fit into the field; full message will still go into details column
        errInfo.Message = Util.CheckLength(message, 250);
        errInfo.Details = details;
        errInfo.MachineName = Environment.MachineName;
        errInfo.LocalTime = localTime != null ? localTime.Value : App.TimeService.Now;
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
        OnErrorLogged(context, new Exception("ClientError: " + message + Environment.NewLine + details));
        return errInfo.Id;
      } catch(Exception logEx) {
        WriteToSystemLog(logEx, message, details);
        Util.WriteToTrace(logEx, "Fatal failure in database error log. See next error log entry for original error.");
        Util.WriteToTrace(message, details, null);
        return Guid.NewGuid();
      }
    }

    public event EventHandler<ErrorLogEventArgs> ErrorLogged;

    private void OnErrorLogged(OperationContext context, Exception ex) {
      var evt = ErrorLogged;
      if(evt != null)
        evt(this, new ErrorLogEventArgs(context, ex));
    }
    #endregion

    private void WriteToSystemLog(Exception logExc, Exception exc) {
      App.SystemLog.Error("Failed to write to error log: " + logExc.Message);
      App.SystemLog.Error(logExc);
      App.SystemLog.Error("(original exception)");
      App.SystemLog.Error(exc);
    }
    private void WriteToSystemLog(Exception logExc, string message, string details) {
      App.SystemLog.Error("Failed to write to error log: " + logExc.Message);
      App.SystemLog.Error(logExc);
      App.SystemLog.Error(" original error: " + message);
      App.SystemLog.Error(details);
    }


  }// ErrorLogModule class


}//ns
