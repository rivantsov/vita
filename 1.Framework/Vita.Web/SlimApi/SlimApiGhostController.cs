using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.ExceptionHandling;
using Vita.Web;

namespace Vita.Web.SlimApi {
  // This controller is a ghost - it is needed only to cause the WebApi to call SlimApiActionSelector.GetActionMapping method,
  // which returns dictionary of all actions in all SlimApi controllers - to register all SlimApi controller methods. 
  // If the solution has only SlimApi controllers (no standard ApiControllers), then Web Api does not find any, and 
  // does not activate internal action mapping code. So we provide one, as a ghost, only to activate the mapping code. 
  // Did not find any better, straightforward method to activate mapping logic. 
  [CheckModelState]
  public sealed class SlimApiGhostController : ApiController {
    //private constructor to prevent instantiation; WebApi still instantiates it, even with private constructor
    internal SlimApiGhostController() { 
    } 
  } //class
}
