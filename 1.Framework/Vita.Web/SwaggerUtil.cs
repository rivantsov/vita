using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using Vita.Web.SlimApi;

namespace Vita.Web {

  /// <summary>Methods used in SwaggerConfig.cs class/file in Web projects. </summary>
  public static class SwaggerUtil {

    public static string GetSwaggerApiGroup(HttpActionDescriptor descriptor) {
      var slimDescr = descriptor as SlimApiActionDescriptor;
      if(slimDescr != null)
        return slimDescr.ControllerInfo.ApiGroup;
      return descriptor.ControllerDescriptor.ControllerType.Name;
    }

    public static string GetSwaggerOperationId(HttpActionDescriptor descriptor) {
      string baseName;
      var slimDescr = descriptor as SlimApiActionDescriptor;
      if(slimDescr != null)
        // We inject ApiGroup in front to allow proper sorting of actions and api groups
        baseName = slimDescr.ControllerInfo.ApiGroup + slimDescr.ControllerInfo.TypeInfo.Name;
      else
        baseName = descriptor.ControllerDescriptor.ControllerType.Name;
      return baseName + "_" + descriptor.ActionName;
    }

  }
}
