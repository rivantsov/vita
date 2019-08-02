using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Services.Implementations;
using Vita.Entities.Utilities;

namespace Vita.Entities.Logging {

  public class InfoLogEntry : LogEntry {
    string _message;
    object[] _args;
    string _formattedMessage;
    
    public InfoLogEntry(LogContext context, string message, params object[] args) : base(LogEntryType.Information, context) {
      _message = message;
      _args = args;
    }

    public override string AsText() {
      _formattedMessage = _formattedMessage ?? Util.SafeFormat(_message, _args);
      return _formattedMessage; 
    }
    public override string ToString() {
      return AsText();
    }
  }//class

}
