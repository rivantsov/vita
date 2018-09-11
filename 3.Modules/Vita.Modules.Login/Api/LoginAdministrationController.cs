using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Modules.Login.Api {
  [ApiRoutePrefix("logins"), LoggedInOnly, Secured, ApiGroup("Login-Administration")]
  public class LoginAdministrationController : SlimApiController {
    ILoginAdministrationService _adminService; 

    public override void InitController(OperationContext context) {
      base.InitController(context);
      _adminService = Context.App.GetService<ILoginAdministrationService>(); 
    }

    /// <summary>Searches user logins using the multi-field criteria. </summary>
    /// <param name="search">Search parameters.</param>
    /// <returns>The matching list of logins.</returns>
    [ApiGet, ApiRoute("")]
    public SearchResults<LoginInfo> SearchLogins([FromUrl] LoginSearch search) {
      var logins = _adminService.SearchLogins(Context, search);
      var result = logins.Convert<ILogin, LoginInfo>(lg => lg.ToModel());
      return result; 
    }

    /// <summary>Returns user login information based on login ID.</summary>
    /// <param name="id">Login ID.</param>
    /// <returns>Login information</returns>
    [ApiGet, ApiRoute("{id}")]
    public LoginInfo GetLogin(Guid id) {
      var session = Context.OpenSession();
      var login = session.GetEntity<ILogin>(id);
      if(login == null)
        return null;
      return login.ToModel(); 
    }

    /// <summary>Sets one-time password for a user. </summary>
    /// <param name="loginId">User login ID.</param>
    /// <returns>An object containing new generated temporary password and expiration period.</returns>
    [ApiPut, ApiRoute("{loginid}/temppassword")]
    public OneTimePasswordInfo SetOneTimePassword (Guid loginId) {
      var session = Context.OpenSession();
      var login = _adminService.GetLogin(session, loginId);
      Context.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found.");
      var loginSettings = Context.App.GetConfig<LoginModuleSettings>();
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
    [ApiPut, ApiRoute("{loginid}/status")]
    public void UpdateStatus(Guid loginId, [FromUrl] LoginStatusUpdate update) {
      var session = Context.OpenSession();
      var login = _adminService.GetLogin(session, loginId);
      Context.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found.");
      _adminService.UpdateStatus(login, update.Disable, update.Suspend);
    }

  }
}//ns
