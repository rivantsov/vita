using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;


namespace Vita.Web {
  /// <summary>Provides automatic validation of ModelState.IsValid flag for ApiController methods.</summary>
  /// <remarks>This attribute makes it easier to handle input deserialization errors.
  /// Generally you should check this.ModelState.IsValid at the start of each controller method that expects deserialized object in the message body. 
  /// To avoid doing this all the time you can put this attribute either on a controller or on a particular method. 
  /// The attribute implementation checks IsValid flag and throws exception if it is false. The WebCallContextHandler will log the exception details
  /// and create the error response. 
  ///</remarks>
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
  public class CheckModelStateAttribute : ActionFilterAttribute {
    public override void OnActionExecuting(HttpActionContext actionContext) {
      base.OnActionExecuting(actionContext);
      if(!actionContext.ModelState.IsValid)
        throw new ModelStateException(actionContext.ModelState); 
    }
  }

}
