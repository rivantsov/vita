using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.Routing;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Entities.Authorization;
using Vita.Entities.Web.Implementation;

namespace Vita.Web.SlimApi {

  internal class SlimApiActionDescriptor : ReflectedHttpActionDescriptor {
    private static readonly object[] _empty = new object[0];
    public readonly ApiControllerInfo ControllerInfo;
    ApiConfiguration _apiConfig;
    MethodInfo _method; 
    ParameterInfo[] _parameterInfos;
    SlimApiActionExecutor _actionExecutor;
    public List<string> RouteTemplates = new List<string>(); 
    public IList<IDirectRouteFactory> RouteFactories = new List<IDirectRouteFactory>(); 
    // the array is not empty if there are parameters embedded in URL. Initially null. 
    private ModelBinderParameterBinding[] _modelParameterBindings;
    private bool _loggedInOnly;
    private bool _secured;
    IAuthorizationService _authService;
    UrlDateTimeHandler _urlDateTimeHandler; 

    public SlimApiActionDescriptor(HttpControllerDescriptor controllerDescriptor, MethodInfo method, 
                                    ApiControllerInfo controllerInfo, ApiConfiguration apiConfig)
                                  : base(controllerDescriptor, method) {
      ControllerInfo = controllerInfo;
      _apiConfig = apiConfig;
      _method = method; 
      _parameterInfos = method.GetParameters();
      SetupUrlDateTimeHandler(); 
      //Figure out HTTP methods
      base.SupportedHttpMethods.Clear(); //POST is added by default, get rid of it 
      var methodAttrs = method.GetAttributes<ApiMethodAttribute>(orSubClass: true);
      if(methodAttrs.Count == 0)
        return; //no ApiGet/Post... attrs, it is not api method
      foreach(var ma in methodAttrs)
        base.SupportedHttpMethods.Add(ma.Method);
      //Routes. first get route prefix on controller
      var globalRoutePrefix = _apiConfig.GlobalRoutePrefix;
      string routePrefix = CombineRoutes(globalRoutePrefix, ControllerInfo.RoutePrefix); 
      // get ApiRoute attributes and create IDirectRouteFactory objects (as RouteAttribute instances)
      var apiRouteAttrs = method.GetAttributes<ApiRouteAttribute>();
      if(apiRouteAttrs.Count == 0) { //If no Route attr, assume it is empty route
        RouteTemplates.Add(routePrefix);
        RouteFactories.Add(new RouteAttribute(routePrefix));
      } else 
      foreach(var ra in apiRouteAttrs) {
        var routeTemplate = CombineRoutes(routePrefix, ra.Template);
        RouteTemplates.Add(routeTemplate);
        RouteFactories.Add(new RouteAttribute(routeTemplate));
      }
      _loggedInOnly = ControllerInfo.Flags.IsSet(ControllerFlags.LoggedInOnly) || method.HasAttribute<LoggedInOnlyAttribute>();
      _secured = ControllerInfo.Flags.IsSet(ControllerFlags.Secured) || method.HasAttribute<SecuredAttribute>();
      if(_secured)
        _loggedInOnly = true; 
    }

    private void SetupUrlDateTimeHandler() {
      // Check for datetime parameters
      var needHandler = _parameterInfos.Any(p => p.ParameterType == typeof(DateTime) || p.ParameterType == typeof(DateTime?));
      // also check for complex object with [FromUrl] attribute
      var fromUrlParam = _parameterInfos.FirstOrDefault(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(FromUrlAttribute)));
      if(fromUrlParam != null) {
        var dateTimeProps = UrlDateTimeHandler.GetDateTimeProperties(fromUrlParam.ParameterType);
        needHandler |= dateTimeProps.Count > 0;
      }
      if(needHandler)
        _urlDateTimeHandler = new UrlDateTimeHandler(fromUrlParam);
    }

    public override Task<object> ExecuteAsync(HttpControllerContext controllerContext, IDictionary<string, object> arguments, System.Threading.CancellationToken cancellationToken) {
      if(cancellationToken.IsCancellationRequested) {
        return GetCancelledTask<object>();
      }
      try {
        var opContext = GetOperationContext(controllerContext);
        //Record controller name, method name in web context
        var webCtx = opContext.WebContext;
        if(webCtx != null) {
          webCtx.ControllerName = ControllerInfo.Type.Name;
          webCtx.MethodName = this.MethodInfo.Name;
          webCtx.RequestUrlTemplate = this.RouteTemplates[0];
        }
        if(_loggedInOnly && opContext.User.Kind == UserKind.Anonymous)
          throw new AuthenticationRequiredException();
        if(_secured && _apiConfig.AuthorizationEnabled)
          CheckAuthorization(opContext);
        //Retrieve controller (singleton or create dynamic instance)
        var contr = GetController(opContext);
        // A hack to fix missing parameter values 
        ReadMissingModelBoundParameters(controllerContext, arguments, opContext);
        object[] argumentValues = PrepareParameters(opContext, arguments, controllerContext);
        _actionExecutor = _actionExecutor ?? CreateActionExecutor(MethodInfo);
        return _actionExecutor.Execute(contr, argumentValues);
      } catch(Exception e) {
        return CreateErrorTask<object>(e);
      }
    }//method

    private void CheckAuthorization(OperationContext context) {
      var auth = context.User.Authority; 
      if(auth == null) {
        if (_authService == null)
          _authService = context.App.GetService<IAuthorizationService>();
        auth = _authService.GetAuthority(context.User);
        Util.Check(auth != null, "Failed to retrieve Authority for user {0}.", context.User.UserName);
        context.User.Authority = auth;
      }
      var accessTypes = auth.GetObjectAccess(context, ControllerInfo.Type);
      var needAccess = AuthorizationModelExtensions.GetAccessType(context.WebContext.HttpMethod);
      if (!accessTypes.IsSet(needAccess))  {
        var msg = StringHelper.SafeFormat("Access denied to API controller method: {0}.{1}; Http method: {2}", 
          _method.DeclaringType, _method.Name,  context.WebContext.HttpMethod);
        throw new AuthorizationException(msg, ControllerInfo.Type, needAccess, false, null);
      }
    }

    private OperationContext GetOperationContext(HttpControllerContext controllerContext) {
      var webCtx = WebHelper.GetWebCallContext(controllerContext.Request);
      var ctx = webCtx == null ? null : webCtx.OperationContext;
      Util.Check(ctx != null, "Failed to retrieve operation context.");
      return ctx; 
    }

    private object GetController(OperationContext opContext) {
      if(ControllerInfo.Instance != null)
        return ControllerInfo.Instance; //singleton
      var controller = Activator.CreateInstance(ControllerInfo.Type);
      var iContrInit = controller as ISlimApiControllerInit;
      if(iContrInit != null) 
        iContrInit.InitController(opContext);
      return controller; 
    }
    //RI: had to copy stuff from base class, because it is private there
    private object[] PrepareParameters(OperationContext opContext, IDictionary<string, object> parameters, 
                         HttpControllerContext controllerContext) {
      // This is on a hotpath, so a quick check to avoid the allocation if we have no parameters.
      if(_parameterInfos.Length == 0) {
        return _empty;
      }

      int parameterCount = _parameterInfos.Length;
      object[] parameterValues = new object[parameterCount];
      for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++) {
        parameterValues[parameterIndex] = ExtractParameterFromDictionary(opContext, _parameterInfos[parameterIndex], parameters, controllerContext);
      }
      opContext.ThrowValidation(); //throw BadRequest if any value converts failed
      return parameterValues;
    }

    private object ExtractParameterFromDictionary(OperationContext context, ParameterInfo parameterInfo, IDictionary<string, object> parameters, HttpControllerContext controllerContext) {
      object value;
      var name = parameterInfo.Name; 
      var paramType = parameterInfo.ParameterType; 
      context.ValidateTrue(parameters.TryGetValue(name, out value), ClientFaultCodes.ValueMissing, name, null, 
           "Missing parameter '{0}' for method {1}.{2}.", name, MethodInfo.DeclaringType, MethodInfo.Name);
      if (value == null) {
        var canBeNull = paramType.IsNullable();
        context.ValidateTrue(canBeNull, ClientFaultCodes.InvalidUrlParameter, name, null,
                 "Parameter '{0}' for method {1}.{2} may not be null.", name, MethodInfo.DeclaringType, MethodInfo.Name);

      } else { //value != null
        var typeMatch = paramType.IsInstanceOfType(value);
        context.ValidateTrue(typeMatch, ClientFaultCodes.InvalidValue, name, value, ClientFaultCodes.InvalidUrlParameter,
            "Invalid value ({0}) for parameter '{1}', method {2}.{3}, expected {4}.",
               value, name, MethodInfo.DeclaringType, MethodInfo.Name, parameterInfo.ParameterType);
        // fixing the problem with Web API date time handling in URL
        if (_urlDateTimeHandler != null)
          value = _urlDateTimeHandler.Convert(value); 
      }
      return value;
    }

    private static SlimApiActionExecutor CreateActionExecutor(MethodInfo methodInfo) {
      if(methodInfo.ContainsGenericParameters) {
        Util.Throw("Cannot call methods with generic parameters. Method: {0}.{1}, type: {2}", methodInfo.ReflectedType, methodInfo.Name);
      }
      return new SlimApiActionExecutor(methodInfo);
    }

    public override System.Collections.ObjectModel.Collection<HttpParameterDescriptor> GetParameters() {
      var pdList = base.GetParameters();
      foreach(var pd in pdList) {
        var fromUrl = pd.GetCustomAttributes<FromUrlAttribute>().FirstOrDefault();
        if(fromUrl != null)
          pd.ParameterBinderAttribute = new FromUriAttribute();
        if(pd.ParameterType == typeof(OperationContext)) {
          pd.ParameterBinderAttribute = new OperationContextBinderAttribute();
        }
      }
      return pdList; 
    }

    #region Fix for URL-bound parameters
    // RI: hack/fix/workaround. With SlimApi hacks, WebApi fails to retrieve parameters embedded in URL (ex: 'api/books/{id}'), 
    // so arguments have null for these parameters. Could not find a way to fix that, so here is workaround, assigning it explicitly in 
    // this method 

    private void ReadMissingModelBoundParameters(HttpControllerContext controllerContext, IDictionary<string, object> arguments, 
                                                 OperationContext operationContext) {
      if(_modelParameterBindings == null) 
        _modelParameterBindings = this.ActionBinding.ParameterBindings.OfType<ModelBinderParameterBinding>().ToArray();

      if(_modelParameterBindings.Length == 0)
        return;
      var routeData = controllerContext.RouteData;
      object paramValue; 
      foreach(var mbpb in _modelParameterBindings) {
        var paramName = mbpb.Descriptor.ParameterName;
        var paramType = mbpb.Descriptor.ParameterType; 
        bool exists = arguments.TryGetValue(paramName, out paramValue) && paramValue != null; 
        if (exists)
          continue; 
        if(TryGetModelBoundParameter(controllerContext.RouteData, paramName, out paramValue)) {
          if(paramValue != null && !paramType.IsInstanceOfType(paramValue))
            paramValue = SafeConvertParameter(operationContext, paramValue, paramType, paramName);
          arguments[paramName] = paramValue;
        }
      }//foreach
      operationContext.ThrowValidation(); 
    }

    private object SafeConvertParameter(OperationContext context, object value, Type toType, string parameterName) {
      try {
        return ConvertHelper.ChangeType(value, toType);
      } catch(Exception ex) {
        context.ValidateTrue(false, ClientFaultCodes.InvalidUrlParameter, parameterName, value, 
          "Invalid parameter {0}: {1}", parameterName, ex.Message);
        return null; 
      }

    }

    private bool TryGetModelBoundParameter(IHttpRouteData routeData, string parameterName, out object paramValue) {
      paramValue = null;
      foreach(var rdv in routeData.Values) {
        var rds = rdv.Value as IHttpRouteData[];
        if(rds == null) continue;
        foreach(var rd in rds) {
          if(rd.Values.TryGetValue(parameterName, out paramValue))
            return true;
        }
      }
      return false;
    }
    #endregion

    static void ThrowClientFault(string code, string message, params object[] args) {
      message = StringHelper.SafeFormat(message, args); 
      var fault = new ClientFault() {Code = code, Message = message};
      var ex = new ClientFaultException(new []{fault});
      throw ex; 
    }
    
    /// <summary>
    /// Returns a canceled Task of the given type. The task is completed, IsCanceled = True, IsFaulted = False.
    /// </summary>
    private static Task<TResult> GetCancelledTask<TResult>() {
      TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
      tcs.SetCanceled();
      return tcs.Task;
    }

    internal static Task<TResult> CreateErrorTask<TResult>(Exception exception) {
      TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
      tcs.SetException(exception);
      return tcs.Task;
    }

    private static string CombineRoutes(string prefix, string route) {
      if(string.IsNullOrWhiteSpace(prefix))
        return route;
      if(string.IsNullOrWhiteSpace(route))
        return prefix; 
      var result = (prefix + "/" + route).Replace("//", "/");
      return result; 
    }


  }//class
}
