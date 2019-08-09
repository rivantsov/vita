using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services;

namespace Vita.Entities.Logging {

  public class Log : ILog {
    string _fileName;
    public int MaxEntries;
    bool _hasErrors = false;
    List<string> _messages = new List<string>();
    object _lock = new object();
    ILogService _globalLog;
    OperationContext _systemContext; 

    public Log(string fileName, int maxEntries = 1000, EntityApp app = null) {
      _fileName = fileName;
      MaxEntries = maxEntries;
      if(app != null) {
        _globalLog = app.GetService<ILogService>();
        _systemContext = new OperationContext(app, UserInfo.System); 
      }
    }

    public void Clear() {
      lock(_lock) {
        _messages.Clear();
        _hasErrors = false;
      }
    }

    public void Info(string message, params object[] args) {
      AddEntry(message, args);
    }
    public void Error(string message, params object[] args) {
      AddEntry("ERROR: " + message, args);
      _hasErrors = true;
    }

    public bool HasErrors {
      get { return _hasErrors; }
    }

    public string GetAllAsText() {
      lock(_lock)
        return string.Join(Environment.NewLine, _messages);
    }

    public void Flush() {
      if(string.IsNullOrWhiteSpace(_fileName))
        return;
      lock(_lock) {
        try {
          var text = GetAllAsText() + Environment.NewLine;
          File.AppendAllText(_fileName, text);
          _messages.Clear(); 
        } catch(Exception ex) {
          var text = "Error saving activation log: " + ex.ToLogString();
          Trace.WriteLine(text);
          //Debugger.Break();
        }

      }
    }

    public string FileName {
      get { return _fileName; }
      set { _fileName = value;  }
    }

    private void AddEntry(string message, object[] args) {
      if(args != null && args.Length > 0)
        message = Util.SafeFormat(message, args);
      lock(_lock) {
        while(_messages.Count >= MaxEntries)
          _messages.RemoveAt(0);
        _messages.Add(message);
      }
    }



  } //class

  public static class ActivationLogUtil {
    public static void CheckErrors(this ILog log, string header = "Startup failed.") {
      if(log.HasErrors) {
        var errors = log.GetAllAsText();
        System.Diagnostics.Trace.WriteLine(header + Environment.NewLine + errors);
        throw new StartupFailureException(header, errors);
      }
    }
  } //class

} //ns 
