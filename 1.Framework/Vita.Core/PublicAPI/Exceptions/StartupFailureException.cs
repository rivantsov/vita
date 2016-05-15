using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vita.Entities {

  /// <summary> Setup/activation failure exception. </summary>
  public class StartupFailureException : Exception {
    public readonly string Log; //activation log

    public StartupFailureException(string message, string log) : base(message) {
      Log = log; 
    }

    public override string ToString() {
      return Message + Environment.NewLine + Log;
    }
  }
}
