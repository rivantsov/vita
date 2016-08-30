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

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Entities.Web.Implementation;

namespace Vita.Web.SlimApi {

  internal class SlimApiActionSelector : ApiControllerActionSelector {
    ApiConfiguration _apiConfig; 
    IDictionary<string, List<SlimApiActionDescriptor>> _actions = 
        new Dictionary<string, List<SlimApiActionDescriptor>>(StringComparer.OrdinalIgnoreCase);
    
    public SlimApiActionSelector(ApiConfiguration apiConfiguration) {
      _apiConfig = apiConfiguration; 
    }

    public override ILookup<string, HttpActionDescriptor> GetActionMapping(HttpControllerDescriptor controllerDescriptor) {
      if(controllerDescriptor.ControllerType == typeof(SlimApiGhostController)) {
        var newActions = new List<HttpActionDescriptor>(); 
        foreach(var contrInfo in _apiConfig.ControllerInfos) {
          var methods = contrInfo.TypeInfo.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
          foreach(var method in methods) {
            var dtype = method.DeclaringType;
            if (dtype == typeof(object)) //skip ToString()
              continue; 
            var action = new SlimApiActionDescriptor(controllerDescriptor, method, contrInfo, _apiConfig);
            if (action.RouteTemplates.Count > 0 && action.SupportedHttpMethods.Count > 0) {
              RegisterAction(action); 
              newActions.Add(action);

            }
          }
        } //foreach ct
        
        var lkp = newActions.ToLookup(a => a.ActionName, StringComparer.OrdinalIgnoreCase);
        return lkp; 
      }
      // otherwise call base
      return base.GetActionMapping(controllerDescriptor);
    }

    public override HttpActionDescriptor SelectAction(HttpControllerContext controllerContext) {
      if(controllerContext.ControllerDescriptor.ControllerType == typeof(SlimApiGhostController)) {
        var httpMethod = controllerContext.Request.Method;
        var subRoutes = controllerContext.RouteData.GetSubRoutes();
        foreach(var sr in subRoutes) {
          var action = FindAction(sr.Route.RouteTemplate, httpMethod);
          if(action != null)
            return action; 
        }
        // Failed to match - throw BadRequest
        // Note: we cannot throw ClientFaultException here - Web Api will catch it and transform into InternalServerError
        // We have to throw HttpResponseException which Web Api will recongnize and pass it up
        var fmt = controllerContext.Request.GetResponseFormatter(typeof(ClientFault[]));
        var badRequest = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var fault = new ClientFault(ClientFaultCodes.InvalidUrlOrMethod, "Failed to match HTTP Method and URL to controller method.");
        badRequest.Content = new ObjectContent<ClientFault[]>(new [] {fault}, fmt);
        throw new HttpResponseException(badRequest);
      } //if ghost controller
      return base.SelectAction(controllerContext);
    }

    private void RegisterAction(SlimApiActionDescriptor action) {
      List<SlimApiActionDescriptor> routeActions;
      foreach(var route in action.RouteTemplates) {
        if(!_actions.TryGetValue(route, out routeActions)) {
          routeActions = new List<SlimApiActionDescriptor>();
          _actions.Add(route, routeActions);
        }
        routeActions.Add(action);
      }
    }

    private SlimApiActionDescriptor FindAction(string route, HttpMethod method) {
      List<SlimApiActionDescriptor> routeActions; 
      if (!_actions.TryGetValue(route, out routeActions))
        return null; 
      foreach(var action in routeActions)
        if (action.SupportedHttpMethods.Contains(method))
          return action;
      return null; 
    }
    
  }//class

}//ns
