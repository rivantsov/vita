using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Vita.Common;
using Vita.Web;

namespace Vita.UnitTests.Web {
  // A sample of classic/traditional controller based on WebApi's ApiController class
  // Note: WebApi finds Api controllers by scanning loaded assemblies. If your controllers live in a separate assembly, 
  // you should make sure that assembly is loaded before you initialize the WebApi. 
  // You can do this by simply executing 'var contrType = typeof(MyController);' in the data service project (host project). 
  // Note: classic controllers do not use global api prefix that you can setup for SlimApiControllers in ApiConfiguration instance. 
  // So we have to add &quot;api&quot; prefix explicitly at controller level. 
  [RoutePrefix("api/classic")]
  public class ClassicWebApiController : BaseApiController {

    [HttpGet, Route("foo")]
    public string Foo(string p1) {
      Util.Check(base.OpContext != null, "Expected OpContext.");
      return "Foo:" + p1;
    }

  }
}
