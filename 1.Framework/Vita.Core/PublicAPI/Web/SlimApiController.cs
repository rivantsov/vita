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

    public virtual void InitController(OperationContext context) {
      Context = context;
      Context.DbConnectionMode = DbConnectionReuseMode.KeepOpen;
    }

  }
}//ns
