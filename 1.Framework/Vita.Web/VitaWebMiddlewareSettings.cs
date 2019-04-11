using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Web {

  [Flags]
  public enum WebOptions {
    None = 0,
    // For Debug mode only - return exception details for fatal exceptions
    ReturnExceptionDetails = 1,
    ReturnBadRequestOnAuthenticationRequired = 1 << 1,

    DefaultDebug = ReturnExceptionDetails | ReturnBadRequestOnAuthenticationRequired,
    DefaultProduction = ReturnBadRequestOnAuthenticationRequired,
  }

  public class VitaWebMiddlewareSettings {

    public WebOptions Options = WebOptions.DefaultDebug;
    public StringSet IgnorePaths = new StringSet();
    public StringSet FilterPaths = new StringSet(); 
    public IList<string> NoLogHeaders = new List<string>(new[] { "Auhorization" });
    public DbConnectionReuseMode ConnectionReuseMode;

    //constructor
    public VitaWebMiddlewareSettings(WebOptions options = WebOptions.DefaultDebug, 
                        DbConnectionReuseMode connectionReuseMode = DbConnectionReuseMode.KeepOpen ) {
      Options = options;
      ConnectionReuseMode = connectionReuseMode;
      //We ignore Swagger paths by default
      IgnorePaths.Add("/swagger");
    }

  }//class
}
