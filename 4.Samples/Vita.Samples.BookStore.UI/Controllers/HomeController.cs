/*<copyright>
Should a copyright go here?
</copyright>*/
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Vita.Samples.BookStore.UI.Controllers {
    //------------------------------------------------------------------------------
    /// <summary>This controller supplies the main view of the site.</summary>
    ///
    /// This file is code generated and should not be modified by hand.
    /// If you need to customize outside of protected areas, add those changes
    /// in another partial class file.  As a last resort (if generated code needs
    /// to be different), change the Status value below to something other than
    /// Generated to prevent changes from being overwritten.
    ///
    /// <CreatedByUserName>INCODE-1\Dave</CreatedByUserName>
    /// <CreatedDate>2/26/2015</CreatedDate>
    /// <Status>Generated</Status>
    //------------------------------------------------------------------------------
    public partial class HomeController : Controller {
        public ActionResult Index() {
            return View();
        }
        public string Config(string setting) {
            return ConfigurationManager.AppSettings[setting];
        }
    }
}