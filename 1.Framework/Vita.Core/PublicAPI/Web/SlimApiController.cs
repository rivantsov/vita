using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Web.Implementation;

namespace Vita.Entities.Web {
  public class SlimApiController : ISlimApiControllerInit {
    protected OperationContext Context;
    static bool _webInitialized;

    public virtual void InitController(OperationContext context) {
      if (!_webInitialized) {
        //Perform one-time notification for first web call 
        context.App.WebInitilialize(context.WebContext);
        _webInitialized = true; 
      }
      Context = context;
      Context.DbConnectionMode = DbConnectionReuseMode.KeepOpen;
    }

  }
}//ns
