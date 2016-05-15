using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;

namespace Vita.Entities.Web.Implementation {

  [Flags]
  public enum ControllerFlags {
    None = 0,
    Secured = 1,
    LoggedInOnly = 1 << 1,
  }


  public class ApiControllerInfo {
    public readonly object Instance; //for singleton
    public readonly Type Type;
    public readonly string RoutePrefix;
    // Flags are set either thru attributes or set during registration 
    public ControllerFlags Flags;

    public ApiControllerInfo(object instance, Type type, string routePrefix = null) {
      Instance = instance; 
      Type = type;
      if(Type == null && Instance != null)
        Type = Instance.GetType(); 
      RoutePrefix = routePrefix;
      //Prefix
      if(RoutePrefix == null) {
        var prefAttr = Type.GetAttribute<ApiRoutePrefixAttribute>();
        if(prefAttr != null)
          RoutePrefix = prefAttr.Prefix; 
      }
      if (type.HasAttribute<LoggedInOnlyAttribute>())
        Flags |= ControllerFlags.LoggedInOnly;
     //Secured
      if (type.HasAttribute<SecuredAttribute>())
        Flags |= ControllerFlags.Secured;
    }
  }

  public class ApiConfiguration {
    public string GlobalRoutePrefix = "api";
    public bool AuthorizationEnabled = true; // you can disable it in debugging
    public readonly ICollection<ApiControllerInfo> ControllerInfos = new List<ApiControllerInfo>();

    public ApiControllerInfo RegisterControllerType(Type controllerType, string routePrefix = null) {
      var info = new ApiControllerInfo(null, controllerType, routePrefix);
      ControllerInfos.Add(info);
      return info; 
    }

    public void RegisterControllerTypes(params Type[] controllerTypes) {
      foreach(var type in controllerTypes)
        RegisterControllerType(type);
    }

    public void RegisterController(object instance, string routePrefix = null) {
      Util.Check(instance != null, "Controller instance may not be null.");
      // Check for wrong method - if user tries to provide object type, not instance
      Util.Check(instance.GetType() != typeof(Type), "Instance parameter may not be Type - use RegisterControllerType(s) methods.");
      var controllerType = instance.GetType(); 
      var info = new ApiControllerInfo(instance, controllerType, routePrefix);
      ControllerInfos.Add(info);
    }

    public ApiControllerInfo GetControllerInfo(Type controllerType) {
      return ControllerInfos.FirstOrDefault(ci => ci.Type == controllerType);
    }

  } //class

  public static class ApiConfigurationExtensions {
    public static bool IsSet(this ControllerFlags flags, ControllerFlags flag) {
      return (flags & flag) != 0; 
    }
  }//class

} //ns
