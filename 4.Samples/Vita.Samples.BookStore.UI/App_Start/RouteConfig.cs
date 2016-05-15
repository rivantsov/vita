using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Vita.Samples.BookStore.UI {
  public partial class RouteConfig {
    public static void RegisterRoutes(RouteCollection routes) {
      routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

      routes.MapRoute(
          name: "settings",
          url: "home/config",
          defaults: new { controller = "Home", action = "Config" });

      routes.MapRoute(
          name: "API",
          url: "api/{controller}/{action}/{id}",
          defaults: new { controller = "Tasks", action = "Index", id = UrlParameter.Optional }
          );

      routes.MapRoute(
          name: "Default",
          url: "{*url}",
          defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
      );
    }
  }
}