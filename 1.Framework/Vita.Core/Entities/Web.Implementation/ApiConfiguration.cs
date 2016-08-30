using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public readonly TypeInfo TypeInfo;
    public readonly string RoutePrefix;
    // Flags are set either thru attributes or set during registration 
    public ControllerFlags Flags;

    /// <summary>Grouping tag for Swagger UI. By default, it last segment of namespace of controller type.</summary>
    public string ApiGroup; 

    public ApiControllerInfo(object instance, Type type, string routePrefix = null) {
      Instance = instance;
      if(type == null && instance != null)
        type = instance.GetType();
      TypeInfo = type.GetTypeInfo();
      RoutePrefix = routePrefix;
      //Prefix
      if(RoutePrefix == null) {
        var prefAttr = TypeInfo.GetAttribute<ApiRoutePrefixAttribute>();
        if(prefAttr != null)
          RoutePrefix = prefAttr.Prefix; 
      }
      if (TypeInfo.HasAttribute<LoggedInOnlyAttribute>())
        Flags |= ControllerFlags.LoggedInOnly;
     //Secured
      if (TypeInfo.HasAttribute<SecuredAttribute>())
        Flags |= ControllerFlags.Secured;
      ApiGroup = GetApiGroup(TypeInfo);
    }

    private static string GetApiGroup(Type controllerType) {
      //check attr
      var grpAttr = controllerType.GetTypeInfo().GetAttribute<ApiGroupAttribute>();
      if(grpAttr != null)
        return grpAttr.Group;
      var group = controllerType.Name.Replace("Controller", string.Empty);
      return group; 
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
      var controllerType = instance.GetType(); 
      //Check if it is a mistake - user is registering controller type; if yes, redirect to other method
      if(typeof(Type).IsAssignableFrom(controllerType)) {
        RegisterControllerType((Type)instance, routePrefix);
        return; 
      }
      var info = new ApiControllerInfo(instance, controllerType, routePrefix);
      ControllerInfos.Add(info);
    }

    public ApiControllerInfo GetControllerInfo(Type controllerType) {
      return ControllerInfos.FirstOrDefault(ci => ci.TypeInfo == controllerType);
    }

  } //class

  public static class ApiConfigurationExtensions {
    public static bool IsSet(this ControllerFlags flags, ControllerFlags flag) {
      return (flags & flag) != 0; 
    }
  }//class

} //ns
