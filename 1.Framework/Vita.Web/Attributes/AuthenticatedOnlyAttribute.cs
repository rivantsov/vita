using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

using Vita.Entities;

namespace Vita.Web {
  /// <summary>For use with classic WebApi's controllers derived from ApiController.</summary>
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
  public class AuthenticatedOnlyAttribute : ActionFilterAttribute {
    public override void OnActionExecuting(HttpActionContext actionContext) {
      base.OnActionExecuting(actionContext);
      var webContext = WebHelper.GetWebCallContext(actionContext.Request);
      var user = webContext.OperationContext.User;
      if(user == null || user.Kind != UserKind.AuthenticatedUser) {
        //TODO: detect if session expired or user was never logged in.
        throw new AuthenticationRequiredException("Authentication Required.");
      }
    }
  }

}
