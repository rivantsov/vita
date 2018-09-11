using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities.Logging;

namespace Vita.Entities.Services.Implementations {

  public class DefaultErrorLogService : IErrorLogService, IEntityServiceBase {
    EntityApp _app; 
    IOperationLogService _operationLog;

    public event EventHandler<AppErrorEventArgs> Error;
    private void OnErrorLogged(OperationContext context, Exception ex) {
      Error?.Invoke(this, new AppErrorEventArgs(context, ex));
    }


    public void Init(EntityApp app) {
      _app = app; 
      _operationLog = app.GetService<IOperationLogService>(); 
    }

    public Guid LogError(Exception exception, OperationContext context = null) {
      if(_operationLog != null) {
        context = context ?? new OperationContext(_app, UserInfo.System);
        var entry = new ErrorLogEntry(context, exception);
        _operationLog.AddEntry(entry);
      }
      OnErrorLogged(context, exception); 
      return Guid.Empty;
    }

    public Guid LogError(string message, string details, OperationContext context = null, DateTime? localTime = null, Guid? errorId = null) {
      if(_operationLog != null) {
        context = context ?? new OperationContext(_app, UserInfo.System);
        var entry = new ErrorLogEntry(context, message + Environment.NewLine + details);
        _operationLog.AddEntry(entry);
      }
      OnErrorLogged(context, new Exception(message + Environment.NewLine + details));
      return Guid.Empty; 
    }

    public void Shutdown() {
      
    }


  }
}
