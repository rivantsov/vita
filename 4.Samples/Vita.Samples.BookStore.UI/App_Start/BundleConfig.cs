using System.Web.Optimization;

namespace Vita.Samples.BookStore.UI {
  public partial class BundleConfig {
    public static void RegisterBundles(BundleCollection bundles) {
      bundles.Add(new ScriptBundle("~/bundles/DomainApp")
          .Include("~/Scripts/angular.min.js")
          .Include("~/Scripts/angular-ui-router.min.js")
          .Include("~/Scripts/angular-ui/ui-bootstrap-tpls.min.js")
          .Include("~/Scripts/angular-validator.min.js")
          .Include("~/Scripts/angular-cookies.min.js")
          .IncludeDirectory("~/Scripts/Controllers", "*.js")
          .IncludeDirectory("~/Scripts/Factories", "*.js")
          .IncludeDirectory("~/Scripts/Services", "*.js")
          .Include("~/Scripts/DomainApp.js"));

      bundles.Add(new StyleBundle("~/Content/css")
          .Include("~/Content/bootstrap.min.css")
          .Include("~/Content/bootstrap-theme.min.css")
          .Include("~/Content/site.css"));

      BundleTable.EnableOptimizations = false;
    }
  }
}