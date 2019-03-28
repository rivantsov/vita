using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Entities;
using Vita.Entities.Api;
using Vita.Modules.Login;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {
  [Route("api/logins"), Authorize(Roles = "StoreAdmin")] 
  public class LoginAdministrationController : BaseApiController {
    ILoginAdministrationService _adminService => OpContext.App.GetService<ILoginAdministrationService>(); 
      
    /// <summary>Searches user logins using the multi-field criteria. </summary>
    /// <param name="search">Search parameters.</param>
    /// <returns>The matching list of logins.</returns>
    [HttpGet, Route("")]
    public SearchResults<LoginInfo> SearchLogins([FromQuery] LoginSearch search) {
      var logins = _adminService.SearchLogins(OpContext, search);
      var result = logins.Convert<ILogin, LoginInfo>(lg => lg.ToModel());
      return result; 
    }

    /// <summary>Returns user login information based on login ID.</summary>
    /// <param name="id">Login ID.</param>
    /// <returns>Login information</returns>
    [HttpGet, Route("{id}")]
    public LoginInfo GetLogin(Guid id) {
      var session = OpContext.OpenSession();
      var login = session.GetEntity<ILogin>(id);
      if(login == null)
        return null;
      return login.ToModel(); 
    }

    /// <summary>Sets one-time password for a user. </summary>
    /// <param name="loginId">User login ID.</param>
    /// <returns>An object containing new generated temporary password and expiration period.</returns>
    [HttpPut, Route("{loginid}/temppassword")]
    public OneTimePasswordInfo SetOneTimePassword (Guid loginId) {
      var session = OpContext.OpenSession();
      var login = _adminService.GetLogin(session, loginId);
      OpContext.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found.");
      var loginSettings = OpContext.App.GetConfig<LoginModuleSettings>();
      string password = _adminService.GenerateTempPassword();
      _adminService.SetOneTimePassword(login, password);
      return new OneTimePasswordInfo() { Password = password, ExpiresHours = (int) loginSettings.OneTimePasswordExpiration.TotalHours }; 
    }

    /// <summary>Login status change object.</summary>
    public class LoginStatusUpdate {
      /// <summary>If true, login will be disabled.</summary>
      public bool? Disable { get; set; }
      /// <summary>If true, login will be suspended.</summary>
      public bool? Suspend { get; set; }
    }

    /// <summary>Updates login status. </summary>
    /// <param name="loginId">Login ID.</param>
    /// <param name="update">Update object.</param>
    [HttpPut, Route("{loginid}/status")]
    public void UpdateStatus(Guid loginId, [FromQuery] LoginStatusUpdate update) {
      var session = OpContext.OpenSession();
      var login = _adminService.GetLogin(session, loginId);
      OpContext.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found.");
      _adminService.UpdateStatus(login, update.Disable, update.Suspend);
    }

  }
}//ns
