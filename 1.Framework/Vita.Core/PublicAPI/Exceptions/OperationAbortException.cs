using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml;

using Vita.Entities.Runtime;

namespace Vita.Entities {
  // Note: OperationAbortedException would be better, but there is already a class in System.Data

  /// <summary> An exception thrown when current operation(s) were aborted due to external error (invalid input) and not
  /// due to system internal failure. </summary>
  /// <remarks>A simple example is a client fault - invalid data submitted from the client. 
  /// Other examples - concurrency violation, unique index violation.
  /// </remarks>
  [Serializable]
  public class OperationAbortException : Exception {
    /// <summary>Indicates if error should be logged as serious error in error log. 
    /// The application code can set this value explicitly after creating exception object. 
    /// In most cases this value is false. For other exceptions (ModelStateException) we might set it to true to log the fact that 
    /// client sumbitted invalid info that failed to deserialize. 
    /// </summary>
    public bool LogAsError = false; 
    /// <summary>
    /// Indicates error type; for standard errors contains one of the constants in the OpeationAbortReasons static class.  
    /// </summary>
    public string ReasonCode;

    public OperationAbortException(string message, string reasonCode, Exception inner = null) : base(message, inner) {
      ReasonCode = reasonCode;
    }

  }//class

  public static class OperationAbortReasons {
    public const string ClientFault = "ClientFault";
    //The following are not used for now
    public const string Deadlock = "Deadlock";
    public const string ConcurrencyViolation = "ConcurrencyViolation";
  }
}
