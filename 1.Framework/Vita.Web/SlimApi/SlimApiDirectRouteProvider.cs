using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Filters;
using System.Web.Http.Routing;

namespace Vita.Web.SlimApi {

  internal class SlimApiDirectRouteProvider : DefaultDirectRouteProvider {

    protected override IReadOnlyList<IDirectRouteFactory> GetActionRouteFactories(System.Web.Http.Controllers.HttpActionDescriptor actionDescriptor) {
      var slimApiAction = actionDescriptor as SlimApiActionDescriptor;
      if(slimApiAction != null)
        return slimApiAction.RouteFactories.ToArray(); 
      return base.GetActionRouteFactories(actionDescriptor).ToList();
    }

  }//class


}//ns
