using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Web;

namespace Vita.Modules.Login.Api {

  [ApiRoutePrefix("mylogin"), LoggedInOnly, ApiGroup("Login/Management")]
  public class LoginSelfServiceController : SlimApiController {
    ILoginManagementService _loginManager; 

    public override void InitController(OperationContext context) {
      base.InitController(context);
      _loginManager = Context.App.GetService<ILoginManagementService>(); 
    }

    /// <summary>Returns login information for the current user. </summary>
    /// <returns>An object with information about login.</returns>
    [ApiGet, ApiRoute("")]
    public LoginInfo GetLoginInfo() {
      var login = GetCurrentLogin();
      return login.ToModel(); 
    }

    /// <summary>Updates login information for the user.</summary>
    /// <param name="loginInfo">Login information.</param>
    /// <returns>Login info object with updated information.</returns>
    /// <remarks>Not all information may be updated with this call.</remarks>
    [ApiPut, ApiRoute("")]
    public LoginInfo UpdateLoginInfo(LoginInfo loginInfo) {
      var login = GetCurrentLogin();
      Context.ThrowIfNull(login, ClientFaultCodes.ContentMissing, "LoginInfo", "LoginInfo is missing.");
      Context.ThrowIf(loginInfo.Id != login.Id, ClientFaultCodes.InvalidValue, "Id", "Invalid login Id, must match current user's login id.");
      _loginManager.UpdateLogin(login, loginInfo);
      return login.ToModel(); 
    }

    /// <summary>Returns the list of all standard secret questions. </summary>
    /// <returns>Full list of secret questions.</returns>
    [ApiGet, ApiRoute("allquestions")]
    public List<SecretQuestion> GetStandardSecretQuestions() {
      var session = Context.OpenSystemSession(); //opening system session, no need for authorization
      var questions = _loginManager.GetAllSecretQuestions(session);
      return questions.Select(q => q.ToModel()).ToList();
    }

    /// <summary>Get the list of secret questions for the current user. </summary>
    /// <returns>The list of questions previously selected by the user.</returns>
    [ApiGet, ApiRoute("questions")]
    public List<SecretQuestion> GetMySecretQuestions() {
      var login = GetCurrentLogin();
      var questions = _loginManager.GetUserSecretQuestions(login);
      return questions.Select(q => q.ToModel()).ToList();
    }

    /// <summary>Sets the answers for the secret questions for the user. </summary>
    /// <param name="answers">A list of answers paired with question ID.</param>
    [ApiPut, ApiRoute("answers")]
    public void SetUserSecretQuestionAnswers(List<SecretQuestionAnswer> answers) {
      var login = GetCurrentLogin();
      _loginManager.UpdateUserQuestionAnswers(login, answers);
      _loginManager.CheckLoginFactorsSetupCompleted(login);
    }

    /// <summary>Changes the order of secret questions for the users.</summary>
    /// <param name="ids">A list of question IDs in desired order.</param>
    [ApiPut, ApiRoute("answers/order")]
    public void ReorderUserQuestionAnswers(IList<Guid> ids) {
      var login = GetCurrentLogin();
      _loginManager.ReorderUserQuestionAnswers(login, ids);
    }

    /// <summary>Returns the list of user extra factors. </summary>
    /// <returns>The list of factor objects.</returns>
    [ApiGet, ApiRoute("factors")]
    public IList<LoginExtraFactor> GetUserFactors() {
      var login = GetCurrentLogin();
      return _loginManager.GetUserFactors(login); 
    }

    /// <summary>Returns an information on user extra factor identified by ID.</summary>
    /// <param name="factorId">Factor ID.</param>
    /// <returns>Factor information.</returns>
    [ApiGet, ApiRoute("factors/{id}")]
    public LoginExtraFactor GetUserFactor(Guid factorId) {
      var login = GetCurrentLogin();
      return _loginManager.GetUserFactor(login, factorId);
    }


    //For GoogleAutenticator, value is ignored and secret is generated
    /// <summary>Adds a new login extra factor (email, SMS) for the user login. </summary>
    /// <param name="factor">Factor information.</param>
    /// <returns>The created factor info object.</returns>
    [ApiPost, ApiRoute("factors")]
    public LoginExtraFactor AddUserFactor(LoginExtraFactor factor) {
      var login = GetCurrentLogin();
      var newFactor = _loginManager.AddFactor(login, factor.Type, factor.Value);
      var session = EntityHelper.GetSession(login);
      session.SaveChanges();
      _loginManager.CheckLoginFactorsSetupCompleted(login); 
      return newFactor; 
    }

    /// <summary>Updates user extra factor. </summary>
    /// <param name="factor">Factor information.</param>
    /// <returns>The updated factor info object.</returns>
    [ApiPut, ApiRoute("factors")]
    public LoginExtraFactor UpdateFactor(LoginExtraFactor factor) {
      var login = GetCurrentLogin();
      var iFactor = login.ExtraFactors.FirstOrDefault(f => f.Id == factor.Id);
      Context.ThrowIfNull(iFactor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", factor.Id);
      if (factor.Type == ExtraFactorTypes.GoogleAuthenticator)
        factor.Value = GoogleAuthenticator.GoogleAuthenticatorUtil.GenerateSecret();
      var updFactor = _loginManager.UpdateFactor(iFactor, factor.Value);
      var session = EntityHelper.GetSession(login);
      session.SaveChanges();
      _loginManager.CheckLoginFactorsSetupCompleted(login);
      return updFactor;
    }

    /// <summary>Returns the URL for Google Authenticator QR page.</summary>
    /// <param name="id">Factor ID.</param>
    /// <returns>The URL of the QR page.</returns>
    /// <remarks>The QR page shows the secret code as a bar code image
    /// that can be scanned by the phone when setting up Google Authenticator app on the phone.</remarks>
    [ApiGet, ApiRoute("factors/{id}/qr")]
    public string GetGoogleAuthenticatorQrUrl(Guid id) {
      var login = GetCurrentLogin();
      var session = EntityHelper.GetSession(login);
      var iFactor = session.GetEntity<ILoginExtraFactor>(id);  
      Context.ThrowIfNull(iFactor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", id);
      //the following should be caught by Authorization
      Context.ThrowIf(iFactor.Login != login, ClientFaultCodes.InvalidAction, "id", "Factor does not belong to current user.");
      Context.ThrowIf(iFactor.FactorType != ExtraFactorTypes.GoogleAuthenticator, ClientFaultCodes.InvalidAction,
          "FactorType", "Factor is not GoogleAuthenticator.");
      return _loginManager.GetGoogleAuthenticatorQRUrl(iFactor); 
    }

    /// <summary>Deletes user extra factor. </summary>
    /// <param name="id">Factor ID.</param>
    [ApiDelete, ApiRoute("factors/{id}")]
    public void DeleteFactor(Guid id) {
      var login = GetCurrentLogin();
      var session = EntityHelper.GetSession(login);
      var factor = login.ExtraFactors.FirstOrDefault(f => f.Id == id);
      Context.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", id);
      session.DeleteEntity(factor); 
      session.SaveChanges();
      _loginManager.CheckLoginFactorsSetupCompleted(login);
    }

    //Email/phone verification
    /// <summary>Tells server to send the PIN to verify the extra factor.</summary>
    /// <param name="factorId">Factor ID.</param>
    [ApiPost, ApiRoute("factors/{factorId}/pin")]
    public void SendPin(Guid factorId) {
      var login = GetCurrentLogin();
      var factor = login.ExtraFactors.FirstOrDefault(f => f.Id == factorId);
      Context.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", factorId);
      var processService = Context.App.GetService<ILoginProcessService>();
      var token = processService.GenerateProcessToken(); 
      var process = processService.StartProcess(login, LoginProcessType.FactorVerification, token);
      var pin = processService.GeneratePin(process, factor);
      processService.SendPin(process, factor);
    }

    /// <summary>Submits the PIN received by user (in email) to verify it. </summary>
    /// <param name="factorId">Factor ID.</param>
    /// <param name="pin">The PIN value.</param>
    /// <returns>True if PIN value matches; otherwise, false.</returns>
    [ApiPut, ApiRoute("factors/{factorid}/pin/{pin}")]
    public bool SubmitPin(Guid factorId, string pin) {
      Context.ThrowIfEmpty(pin, ClientFaultCodes.ValueMissing, "pin", "Pin value missing");
      var login = GetCurrentLogin();
      var factor = login.ExtraFactors.FirstOrDefault(f => f.Id == factorId);
      Context.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", factorId);
      var processService = Context.App.GetService<ILoginProcessService>();
      var processes = processService.GetActiveConfirmationProcesses(factor, LoginProcessType.FactorVerification);
      Context.ThrowIf(processes.Count == 0, ClientFaultCodes.ObjectNotFound, "LoginProcess", "Confirmation process not found or expired.");
      foreach(var process in processes)
        if (processService.SubmitPin(process, pin))
          return true;
      return false;
    }

    /// <summary>Changes user password. </summary>
    /// <param name="changeInfo">Change information containing old and new passwords.</param>
    [ApiPut, ApiRoute("password")]
    public void ChangePassword(PasswordChangeInfo changeInfo) {
      Context.WebContext.MarkConfidential();
      Context.ThrowIfNull(changeInfo, ClientFaultCodes.ContentMissing, "Password", "Password change info is missing.");
      var login = GetCurrentLogin();
      _loginManager.ChangePassword(login, changeInfo.OldPassword, changeInfo.NewPassword);
      var session = EntityHelper.GetSession(login);
      session.SaveChanges();
    }

    //We could handle PUT and POST in one method, but swagger creates two entries with the same operation_id
    // and this crushes the API Client generator (AutoRest) - in case somebody wants to use this tool
    /// <summary>Registers a user device. </summary>
    /// <param name="device">Devie information.</param>
    /// <returns>The device info object.</returns>
    /// <remarks>Registering device allows special login modes when logged in from this device. 
    /// For example, user might enable multi-factor login, but for his home computer would 
    /// set to skip multi-factor process. In this case the home computer should be registered on the server, 
    /// the device token should be saved in the browser, and then sent along with username and password
    /// when user logs in. </remarks>
    [ApiPost, ApiRoute("device")]
    public DeviceInfo RegisterDevice(DeviceInfo device) {
      return RegisterOrUpdateDevice(device); 
    }

    /// <summary>Updates user (client) device information. </summary>
    /// <param name="device">The device information.</param>
    /// <returns>The updated device information.</returns>
    [ApiPut, ApiRoute("device")]
    public DeviceInfo UpdateDevice(DeviceInfo device) {
      return RegisterOrUpdateDevice(device);
    }

    private DeviceInfo RegisterOrUpdateDevice(DeviceInfo device) {
      var login = GetCurrentLogin();
      var session = EntityHelper.GetSession(login);
      ITrustedDevice deviceEnt = null;
      if(!string.IsNullOrWhiteSpace(device.Token))
        deviceEnt = login.GetDevice(device.Token);
      if(deviceEnt == null)
        deviceEnt = _loginManager.RegisterTrustedDevice(login, device.Type, device.TrustLevel);
      else {
        deviceEnt.TrustLevel = device.TrustLevel;
      }
      session.SaveChanges();
      return new DeviceInfo() { Token = deviceEnt.Token, TrustLevel = deviceEnt.TrustLevel, Type = deviceEnt.Type };
    }

    [ApiDelete, ApiRoute("device/{token}")]
    public void DeleteDevice(string token) {
      var login = GetCurrentLogin();
      var deviceEnt = login.GetDevice(token);
      if(deviceEnt == null)
        return;
      var session = EntityHelper.GetSession(login);
      session.DeleteEntity(deviceEnt);
      session.SaveChanges(); 
    }


    private ILogin GetCurrentLogin() {
      var session = OpenSession();
      var login = _loginManager.GetLogin(session);
      Context.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found for user: {0}.", Context.User.UserName);
      return login; 
    }

    private IEntitySession OpenSession() {
      //TODO: maybe change to secure session; so far not necessary, everything in this controller happens in connection to currently logged in user.
      return Context.OpenSession(); 
    }
  }
}
