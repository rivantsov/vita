using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  public interface ILogFileWriter {
    void WriteLines(IList<string> lines);
  }

  public class LogFileWriter: ILogFileWriter {
    string _fileName;
    static object _fileWriteLock = new object(); 

    public LogFileWriter(string fileName, string startMessage = null) {
      _fileName = fileName;
      if(startMessage != null)
        WriteLines(new[] { startMessage }); 
    }

    public void WriteLines(IList<string> lines) {
      try {
        // We use AppendAllLines to ensure NewLine between chunks
        lock(_fileWriteLock)
          File.AppendAllLines(_fileName, lines);
      } catch(Exception ex) {
        LastResortErrorLog.Instance.LogFatalError(ex.ToLogString(), string.Empty);
      }
    }

  }
}
