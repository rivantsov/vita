using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Web.Http;

using Vita.Common;
using Vita.Entities.Services;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Web;

namespace Vita.Web {
  // Base Api controller


  /// <summary>Base controller integrated with VITA functionality. Based on WebApi's ApiController class. </summary>
  /// <remarks><para>Use this class as a base for your custom controllers if you want to use 'classic' api controllers that are heavily 
  /// dependent on WebApi infrastructure. </para>
  /// <para>An alternative is to use <c>Vita.Entities.Api.SlimApiContoller</c> - this base controller class is not dependent on WebApi assemblies, 
  /// so it might be hosted in a business logic assembly.</para>
  /// <para> Note: WebApi finds Api controllers by scanning loaded assemblies. If your controllers live in a separate assembly, 
  /// you should make sure that assembly is loaded before you initialize the WebApi. 
  /// You can do this by simply executing the following code for just one of the controller types from external assembly: </para>
  /// <code>
  ///   var contrType = typeof(MyController);
  /// </code>
  /// </remarks>
  [CheckModelState] //ensures that exception is thrown automatically if there were failures in request data deserialization. 
  public class BaseApiController : ApiController {
    protected OperationContext OpContext; 
    protected WebCallContext WebContext;
    protected IErrorLogService ErrorLog; 

    protected override void Initialize(System.Web.Http.Controllers.HttpControllerContext controllerContext) {
      //Note: when exception is thrown here, it is not routed to exc filter, everything just crashes,
      // so be careful not to throw anything in controller.Initialize
      base.Initialize(controllerContext);
      try {
        WebContext = WebHelper.GetWebCallContext(this);
        if(WebContext == null)
          return; 
        OpContext = WebContext.OperationContext;
        ErrorLog = OpContext.App.GetService<IErrorLogService>();
      } catch(Exception ex) {
        System.Diagnostics.Trace.WriteLine("Exception in controller.Initialize: " + ex.ToLogString());
        if(ErrorLog != null)
          ErrorLog.LogError(ex, OpContext);
      }
    }
    protected virtual IEntitySession OpenSession() {
      return OpContext.OpenSession();
    }

    protected virtual ISecureSession OpenSecureSession() {
      return OpContext.OpenSecureSession();
    }


  }//class
}
