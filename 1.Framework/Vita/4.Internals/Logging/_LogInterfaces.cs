﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vita.Entities.Logging {

  public interface ILog {
    void AddEntry(LogEntry entry);
    void Flush(); 
  }

  public interface ILogListener : ILog { }

  public interface ILogService : ILog {
    void AddListener(ILogListener listener, Func<LogEntry, bool> filter = null);
    void RemoveListener(ILogListener listener);
  }

  public interface IBufferingLog : ILog {
    IList<LogEntry> GetAll(); 
  }


}
