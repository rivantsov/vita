using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities.Utilities;

namespace Vita.Entities {

  /// <summary> Startup/activation failure exception. </summary>
  public class StartupFailureException : Exception {
    public readonly string Log; //activation log

    public StartupFailureException(string message, string log) : base(message + " : " + StringHelper.TrimLength(log, 100)) {
      Log = log; 
    }

    public override string ToString() {
      return Message + Environment.NewLine + Log;
    }
  }
}
