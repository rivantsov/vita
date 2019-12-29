using Microsoft.AspNetCore.Mvc;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {

  /// <summary>Base controller integrated with VITA functionality. </summary>
  [ApiController]
  public class BaseApiController : ControllerBase {

    protected OperationContext OpContext => WebContext.OperationContext;

    protected WebCallContext WebContext {
      get {
        if (_webContext == null)
          Init(); 
        return _webContext; 
      }
    } WebCallContext _webContext; 

    protected virtual void Init() {
      Util.Check(this.ControllerContext != null, "Controller not initialized, ControllerContext is null");
      _webContext = base.HttpContext.GetWebCallContext();
    }

    protected virtual IEntitySession OpenSession() {
      return OpContext.OpenSession();
    }

  }//class
}
