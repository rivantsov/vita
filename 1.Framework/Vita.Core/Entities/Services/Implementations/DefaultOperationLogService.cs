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

  public class DefaultOperationLogService : IOperationLogService {
    ITimerService _timerService;
    ConcurrentQueue<OperationLogEntry> _entries = new ConcurrentQueue<OperationLogEntry>(); 

    public DefaultOperationLogService(EntityApp app, LogLevel logLevel = Services.LogLevel.Details) {
      LogLevel = logLevel;
      _timerService = app.GetService<ITimerService>();
      _timerService.Elapsed1Second += TimerService_Elapsed1Second;
      app.AppEvents.FlushRequested += Events_FlushRequested;
    }

    void TimerService_Elapsed1Second(object sender, EventArgs e) {
      Flush(); 
    }
    void Events_FlushRequested(object sender, EventArgs e) {
      Flush();
    }

    #region IOperationLogService members
    public LogLevel LogLevel {get; set;}

    public void Log(OperationLogEntry entry) {
      OperationLogEntry dummy;
      while(_entries.Count > 100)
        _entries.TryDequeue(out dummy);

      switch(entry.EntryType) {
        case LogEntryType.Information: 
        case LogEntryType.Command:
          if(this.LogLevel == Services.LogLevel.Details)
            _entries.Enqueue(entry); 
          break; 
        case LogEntryType.Error:
          _entries.Enqueue(entry);
          break; 
      }
    }

    public void Flush() {
      if(Saving == null)
        return; 
      var entries = new List<LogEntry>(); 
      OperationLogEntry entry;
      while(_entries.TryDequeue(out entry))
        entries.Add(entry);
      var byUserId = entries.GroupBy(e => e.UserName);
      foreach(var g in byUserId) {
        var text = string.Join(Environment.NewLine, g);
        if (Saving != null) {
          Saving(this, new LogSaveEventArgs("--User: " + g.Key + Environment.NewLine + text));
        }
        //Trace.WriteLine(text); 
      }
    }
    
    public event EventHandler<LogSaveEventArgs> Saving;
   
    #endregion


  }
}
