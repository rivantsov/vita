using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;


namespace Vita.Entities {
  /// <summary>Exception indicating that non-authenticated user attempted to call a method that requires authentication.
  /// Thrown by AuthenticatedOnlyAttribute implementation.
  /// </summary>
  public class AuthenticationRequiredException : OperationAbortException {
    public AuthenticationRequiredException(string message = "Authentication required") : base(message, ClientFaultCodes.AuthenticationRequired) { }
  }

}
