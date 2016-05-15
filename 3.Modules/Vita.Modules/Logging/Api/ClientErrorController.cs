using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Entities.Services; 

namespace Vita.Modules.Logging.Api {

  // Controller for saving client-side errors on server (in javascript) on the server.  
  [ApiRoutePrefix("clienterrors")]
  public class ClientErrorController : SlimApiController {

    [ApiPost, ApiRoute("")]
    public Guid PostClientError(ClientError error) {
      Context.ThrowIfNull(error, ClientFaultCodes.ContentMissing, "error", "Error object is missing.");
      var errLog = Context.App.GetService<IErrorLogService>();
      Context.ThrowIfNull(errLog, ClientFaultCodes.ObjectNotFound, "ErrorLog", "Error log service not configured on server.");
      var errId = errLog.LogClientError(this.Context, error.Id, error.Message, error.Details, error.AppName, error.LocalTime);
      return errId; 
    }
  }
}
