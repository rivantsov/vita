using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Entities.Logging {

  public class DefaultOperationLog : ILog {
    OperationContext _context;
    ILogService _logService; 

    public DefaultOperationLog(OperationContext context) {
      _context = context;
      _logService = _context.App.GetService<ILogService>(); 
    } 
    public void AddEntry(LogEntry entry) {
      _logService.AddEntry(entry); 
    }
  }
}
