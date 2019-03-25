using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Entities.Services;
using Vita.Entities;
using Vita.Entities.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Vita.Web {

  /// <summary>Base controller integrated with VITA functionality. Based on WebApi's ControllerBase class. </summary>
  /// <remarks><para>Use this class as a base for your custom controllers if you want to use 'classic' api controllers that are heavily 
  /// dependent on WebApi infrastructure. </para>
  /// </remarks>
  //[CheckModelState] //ensures that exception is thrown automatically if there were failures in request data deserialization. 
  // ApiController is optional, we use it here for one effect - the [FromBody] attribute will be 
  // automatically inferred for complex input parameters
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

    public virtual void Init() {
      Util.Check(this.ControllerContext != null, "Controller not initialized, ControllerContext is null");
      _webContext = base.HttpContext.GetWebCallContext(); 
    }

    protected virtual IEntitySession OpenSession() {
      return OpContext.OpenSession();
    }

  }//class
}
