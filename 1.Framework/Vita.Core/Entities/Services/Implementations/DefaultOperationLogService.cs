using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Logging;

namespace Vita.Entities.Services.Implementations {

  public class DefaultOperationLogService : IOperationLogService, IEntityService {
    IBackgroundSaveService _saveService; 
    ConcurrentQueue<LogEntry> _entries = new ConcurrentQueue<LogEntry>(); 

    public DefaultOperationLogService(EntityApp app, LogLevel logLevel = Services.LogLevel.Details) {
      LogLevel = logLevel;
    }

    #region IEntityService members
    public void Init(EntityApp app) {
      _saveService = app.GetService<IBackgroundSaveService>();
    }

    public void Shutdown() {
    }
    #endregion 


    #region IOperationLogService members
    public LogLevel LogLevel {get; set;}

    public void Log(LogEntry entry) {
      if(_saveService == null)
        return; 
      LogEntry dummy;
      while(_entries.Count > 1000)
        _entries.TryDequeue(out dummy);

      switch(entry.EntryType) {
        case LogEntryType.Information: 
        case LogEntryType.Command:
          if(this.LogLevel == Services.LogLevel.Details)
            _saveService.AddObject(entry); 
          break; 
        case LogEntryType.Error:
          _saveService.AddObject(entry); 
          break; 
      }
    }
    #endregion


  }
}
