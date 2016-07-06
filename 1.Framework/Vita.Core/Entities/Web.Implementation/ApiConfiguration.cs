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

    /// <summary>Grouping tag for Swagger UI. By default, it last segment of namespace of controller type.</summary>
    public string ApiGroup; 

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
      ApiGroup = GetApiGroup(Type);
    }

    private static string GetApiGroup(Type controllerType) {
      //check attr
      var grpAttr = controllerType.GetAttribute<ApiGroupAttribute>();
      if(grpAttr != null)
        return grpAttr.Group; 
      // For ApiGroup, select the last segment of namespace; if it is 'api', then take previous segment
      // ex: Vita.Modules.Login.Api -> Login
      var segms = controllerType.Namespace.Split('.');
      var group = segms[segms.Length - 1];
      if(group.Equals("api", StringComparison.InvariantCultureIgnoreCase) && segms.Length > 1)
        group = segms[segms.Length - 2];
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
      return ControllerInfos.FirstOrDefault(ci => ci.Type == controllerType);
    }

  } //class

  public static class ApiConfigurationExtensions {
    public static bool IsSet(this ControllerFlags flags, ControllerFlags flag) {
      return (flags & flag) != 0; 
    }
  }//class

} //ns
