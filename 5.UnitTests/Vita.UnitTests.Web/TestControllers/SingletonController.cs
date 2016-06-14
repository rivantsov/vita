using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;

namespace Vita.UnitTests.Web {

  [ApiRoutePrefix("singleton")]
  public class SingletonController {
    string _someConfig; 

    public SingletonController(string someConfig) {
      _someConfig = someConfig; 
    }

    [ApiGet, ApiRoute("foo")]
    public string Foo(OperationContext context, string p1) {
      Util.Check(context != null, "Operation context not provided");
      return "Foo:" + p1;
    }
  }
}
