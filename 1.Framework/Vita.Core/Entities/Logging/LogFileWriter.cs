using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Services;

namespace Vita.Entities.Logging {

  /// <summary>Listens to OperationLogService (which writes log to database) and copies all log entries to text file.</summary>
  internal class LogFileWriter {
    public string LogPath;
    EntityApp _app;
    IOperationLogService _logService;
    private object _lock = new object();

    public LogFileWriter(EntityApp app, string logPath) {
      _app = app;
      LogPath = logPath; 
      _logService = app.GetService<IOperationLogService>();
      Util.Check(_logService != null, "OperationLog service not registered, cannot attach LogFileWriter.");
      if (_logService != null)
        _logService.Saving += logService_Saving;
    }

    void logService_Saving(object sender, LogSaveEventArgs e) {
      if(string.IsNullOrWhiteSpace(LogPath))
        return; 
      try {
        lock(_lock) {
          System.IO.File.AppendAllText(LogPath, e.Text); //e.Text already includes NewLine, no need to add it here
        }
      } catch(Exception ex) {
        Util.WriteToTrace(ex, e.Text, copyToEventLog: false);
      }
    }

    internal void Disconnect() {
      if (_logService != null)
        _logService.Saving -= logService_Saving;
      LogPath = null; 
    }
  }
}
