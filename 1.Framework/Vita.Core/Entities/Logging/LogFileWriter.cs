using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Services;

namespace Vita.Entities.Logging {

  /// <summary>Listens to BackgroundSaveService (which writes log to database) and copies all log entries to text file.</summary>
  internal class LogFileWriter {
    EntityApp _app;
    IOperationLogService _logService;
    IBackgroundSaveService _saveService; 
    private object _lock = new object();
    string _logPath;
    string _fullPath; 

    public LogFileWriter(EntityApp app, string logPath) {
      _app = app;
      LogPath = logPath; 
      _logService = app.GetService<IOperationLogService>();
      _saveService = app.GetService<IBackgroundSaveService>();
      _saveService.Saving += SaveService_Saving;
    }

    public string LogPath {
      get { return _logPath; }
      set {
        _logPath = value;
        _fullPath = Util.GetFullAppPath(_logPath);
      }
    } 

    private void SaveService_Saving(object sender, BackgroundSaveEventArgs e) {
      if(string.IsNullOrWhiteSpace(_logPath))
        return; 
      var entries = e.Entries.OfType<LogEntry>().ToList();
      if(entries.Count == 0)
        return;
      var text = string.Join(Environment.NewLine, entries);
      // Here is the problem. We could use _logPath (filename only) directly in File.AppendAllText, it works in normal call, current dir is assigned to bin folder. 
      // But when we are flushing in response to Domain_Unload event, the current directory is changed (when running inside VS - to VStudio bin folder)
      // So we have to specify fuill path explicitly
      lock(_lock) {
        File.AppendAllText(_fullPath, text); 
      }
    }

  }
}
