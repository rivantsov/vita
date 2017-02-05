using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;

namespace Vita.Entities.Logging {

  public class SystemLog {
    public bool HasErrors { get; private set; }
    IList<string> _entries = new List<string>();
    object _lock = new object(); 

    public SystemLog(string logFile = null) {
      LogFile = logFile;
    }

    public void Info(string message, params object[] args) {
      var msg = StringHelper.SafeFormat(message, args);
      lock(_lock) {
        _entries.Add(msg);
        WriteToFile(msg); 
      }
    }

    public string LogFile {
      get { return _logFile; }
      set {
        _logFile = value;
        _fullLogFilePath = Util.GetFullAppPath(_logFile); 
      }
    }
    string _logFile;
    string _fullLogFilePath; 


    public void Error(string message, params object[] args) {
      HasErrors = true;
      Info(message, args); 
    }
    public void Error(Exception exc) {
      HasErrors = true;
      var msg = exc.ToLogString(); 
      Info(msg);
    }

    public string GetAllAsText () {
      lock(_lock) {
        return string.Join(Environment.NewLine, _entries);
      }
    }

    private void WriteToFile(string message) {
      if(string.IsNullOrWhiteSpace(_fullLogFilePath))
        return; 
      File.AppendAllText(_fullLogFilePath, message + Environment.NewLine);
    }
    
  }
}
