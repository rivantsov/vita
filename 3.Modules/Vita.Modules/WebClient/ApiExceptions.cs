using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.WebClient {

  public class ApiException : Exception {
    public readonly HttpStatusCode Status;
    public object Details; 
    public ApiException(string message, HttpStatusCode status, object details = null) : base(message) {
      Status = status;
      Details = details; 
    }
    public override string ToString() {
      return StringHelper.SafeFormat(@"{0} Status: {1}, Details: {2}", Message, Status, Details);
    }
  }

  public class BadRequestException : ApiException {
    public BadRequestException( object customErrors) : base("BadRequest status returned by service.", HttpStatusCode.BadRequest, customErrors) {
    }
    public override string ToString() {
      return StringHelper.SafeFormat(@"BadRequest, errors:
{1}", Details);
    }
  }//class


}
