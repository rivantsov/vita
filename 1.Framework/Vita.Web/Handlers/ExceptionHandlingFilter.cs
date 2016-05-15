using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Web {

  /// <summary>
  /// Saves exception in WebContext object to be later processed by the WebMessageHandler. Add this filter to message filters in web app configuration code. 
  /// </summary>
  public class ExceptionHandlingFilter : ExceptionFilterAttribute {
    
    public override void OnException(HttpActionExecutedContext actionExecutedContext) {
      var exc = actionExecutedContext.Exception;
      Trace.WriteLine("Web server exception: " + exc.ToLogString());
      var webContext = actionExecutedContext.Request.GetWebCallContext();
      if(webContext != null) 
        webContext.Exception = exc;
    }
    
  } //class



}
