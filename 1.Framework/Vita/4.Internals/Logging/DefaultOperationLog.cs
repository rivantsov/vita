using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public class DefaultOperationLog : ILog {
    ILogService _logService; 

    public DefaultOperationLog(OperationContext context) {
      _logService = context.App.GetService<ILogService>(); 
    } 
    public void AddEntry(LogEntry entry) {
      _logService.AddEntry(entry); 
    }
  }
}
