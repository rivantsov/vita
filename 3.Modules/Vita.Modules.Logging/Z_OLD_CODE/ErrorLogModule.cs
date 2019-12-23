using System;

using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Modules.Logging {

  public class ErrorLogModule : EntityModule, IErrorLogService {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public ErrorLogModule(EntityArea area) : base(area, "ErrorLog", "Error Logging Module.", version: CurrentVersion) {
      RegisterEntities(typeof(IErrorLog));
      App.RegisterService<IErrorLogService>(this);
    }
    public override  void Init() {
      base.Init();
    }

    #region IErrorLogService Members

    public Guid LogError(Exception exception, OperationContext context = null) {
      if(!this.App.IsConnected()) {
        OnErrorLogged(context, exception);
        Util.WriteToTrace(exception, context.GetLogContents(), copyToEventLog: true);
        return Guid.Empty;
      }
      try {
        var session = this.App.OpenSystemSession();
        var errInfo = session.NewEntity<IErrorLog>();
        errInfo.CreatedOn = App.TimeService.UtcNow;
        errInfo.LocalTime = App.TimeService.Now;
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
            errInfo.WebCallId = context.WebContext.Id; 
        }
        session.SaveChanges();
        OnErrorLogged(context, exception);
        return errInfo.Id;
      } catch (Exception logEx) {
        Util.WriteToTrace(logEx, "Fatal failure in database error log. See next error log entry for original error.");
        Util.WriteToTrace(exception, null, copyToEventLog: true);
        return Guid.NewGuid(); 
      }
    }
    public Guid LogError(string message, string details, OperationContext context = null) {
      if(!this.App.IsConnected()) {
        OnErrorLogged(context, new Exception(message + Environment.NewLine + details));
        Util.WriteToTrace(message, details, context.GetLogContents(), copyToEventLog: true);
        return Guid.Empty; 
      }
      try {
        var session = this.App.OpenSystemSession();
        session.DisableStoredProcs(); //as a precaution, taking simpler path, in case something wrong with stored procs 
        var errInfo = session.NewEntity<IErrorLog>();
        errInfo.ExceptionType = "Unknown";
        //Some messages might be really long; check length to fit into the field; full message will still go into details column
        errInfo.Message = Util.CheckLength(message, 250);
        errInfo.Details = details;
        errInfo.MachineName = Environment.MachineName;
        errInfo.LocalTime = App.TimeService.Now;
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
        Util.WriteToTrace(logEx, "Fatal failure in database error log. See next error log entry for original error.");
        Util.WriteToTrace(message, details, null, copyToEventLog: true);
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
        Util.WriteToTrace(logEx, "Fatal failure in database error log. See next error log entry for original error.");
        Util.WriteToTrace(message, details, null, copyToEventLog: true);
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

  }// ErrorLogModule class


}//ns
