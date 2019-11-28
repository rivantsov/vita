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

namespace Vita.Modules.Login.Api {

  [Route("api/mylogin"), Authorize]
  public class LoginSelfServiceController : BaseApiController {

     ILoginManagementService LoginManager {
      get {
        _loginManager = _loginManager ?? OpContext.App.GetService<ILoginManagementService>();
        return _loginManager; 
      }
    } ILoginManagementService _loginManager;

    /// <summary>Returns login information for the current user. </summary>
    /// <returns>An object with information about login.</returns>
    [HttpGet, Route("")]
    public LoginInfo GetLoginInfo() {
      var session = OpenSession(); 
      var login = GetCurrentLogin(session);
      return login.ToModel(); 
    }

    /// <summary>Updates login information for the user.</summary>
    /// <param name="loginInfo">Login information.</param>
    /// <returns>Login info object with updated information.</returns>
    /// <remarks>Not all information may be updated with this call.</remarks>
    [HttpPut, Route("")]
    public LoginInfo UpdateLoginInfo([FromBody] LoginInfo loginInfo) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      OpContext.ThrowIfNull(login, ClientFaultCodes.ContentMissing, "LoginInfo", "LoginInfo is missing.");
      OpContext.ThrowIf(loginInfo.Id != login.Id, ClientFaultCodes.InvalidValue, "Id", "Invalid login Id, must match current user's login id.");
      LoginManager.UpdateLogin(login, loginInfo);
      session.SaveChanges(); 
      return login.ToModel(); 
    }

    /// <summary>Returns the list of all standard secret questions. </summary>
    /// <returns>Full list of secret questions.</returns>
    [HttpGet, Route("allquestions")]
    public List<SecretQuestion> GetStandardSecretQuestions() {
      var session = OpContext.OpenSession(); //opening system session, no need for authorization
      var questions = LoginManager.GetAllSecretQuestions(session);
      return questions.Select(q => q.ToModel()).ToList();
    }

    /// <summary>Get the list of secret questions for the current user. </summary>
    /// <returns>The list of questions previously selected by the user.</returns>
    [HttpGet, Route("questions")]
    public List<SecretQuestion> GetMySecretQuestions() {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      var questions = LoginManager.GetUserSecretQuestions(login);
      return questions.Select(q => q.ToModel()).ToList();
    }

    /// <summary>Sets the answers for the secret questions for the user. </summary>
    /// <param name="answers">A list of answers paired with question ID.</param>
    [HttpPut, Route("answers")]
    public void SetUserSecretQuestionAnswers([FromBody]SecretQuestionAnswer[] answers) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      LoginManager.UpdateUserQuestionAnswers(login, answers);
    }

    /// <summary>Changes the order of secret questions for the users.</summary>
    /// <param name="ids">A list of question IDs in desired order.</param>
    [HttpPut, Route("answers/order")]
    public void ReorderUserQuestionAnswers([FromBody] Guid[] ids) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      LoginManager.ReorderUserQuestionAnswers(login, ids);
      session.SaveChanges(); 
    }

    /// <summary>Returns the list of user extra factors. </summary>
    /// <returns>The list of factor objects.</returns>
    [HttpGet, Route("factors")]
    public IList<LoginExtraFactor> GetUserFactors() {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      return LoginManager.GetUserFactors(login); 
    }

    /// <summary>Returns an information on user extra factor identified by ID.</summary>
    /// <param name="factorId">Factor ID.</param>
    /// <returns>Factor information.</returns>
    [HttpGet, Route("factors/{id}")]
    public LoginExtraFactor GetUserFactor(Guid factorId) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      return LoginManager.GetUserFactor(login, factorId);
    }


    //For GoogleAutenticator, value is ignored and secret is generated
    /// <summary>Adds a new login extra factor (email, SMS) for the user login. </summary>
    /// <param name="factor">Factor information.</param>
    /// <returns>The created factor info object.</returns>
    [HttpPost, Route("factors")]
    public LoginExtraFactor AddUserFactor([FromBody] LoginExtraFactor factor) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      var newFactor = LoginManager.AddFactor(login, factor.Type, factor.Value);
      session.SaveChanges();
      LoginManager.CheckLoginFactorsSetupCompleted(login);
      return newFactor; 
    }

    /// <summary>Updates user extra factor. </summary>
    /// <param name="factor">Factor information.</param>
    /// <returns>The updated factor info object.</returns>
    [HttpPut, Route("factors")]
    public LoginExtraFactor UpdateFactor([FromBody] LoginExtraFactor factor) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      var iFactor = login.ExtraFactors.FirstOrDefault(f => f.Id == factor.Id);
      OpContext.ThrowIfNull(iFactor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", factor.Id);
      if (factor.Type == ExtraFactorTypes.GoogleAuthenticator)
        factor.Value = Modules.Login.GoogleAuthenticator.GoogleAuthenticatorUtil.GenerateSecret();
      var updFactor = LoginManager.UpdateFactor(iFactor, factor.Value);
      session.SaveChanges();
      LoginManager.CheckLoginFactorsSetupCompleted(login);
      return updFactor;
    }

    /// <summary>Returns the URL for Google Authenticator QR page.</summary>
    /// <param name="id">Factor ID.</param>
    /// <returns>The URL of the QR page.</returns>
    /// <remarks>The QR page shows the secret code as a bar code image
    /// that can be scanned by the phone when setting up Google Authenticator app on the phone.</remarks>
    [HttpGet, Route("factors/{id}/qr")]
    public string GetGoogleAuthenticatorQrUrl(Guid id) {
      var session = OpenSession(); 
      var login = GetCurrentLogin(session);
      var iFactor = session.GetEntity<ILoginExtraFactor>(id);  
      OpContext.ThrowIfNull(iFactor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", id);
      //the following should be caught by Authorization
      OpContext.ThrowIf(iFactor.Login != login, ClientFaultCodes.InvalidAction, "id", "Factor does not belong to current user.");
      OpContext.ThrowIf(iFactor.FactorType != ExtraFactorTypes.GoogleAuthenticator, ClientFaultCodes.InvalidAction,
          "FactorType", "Factor is not GoogleAuthenticator.");
      return LoginManager.GetGoogleAuthenticatorQRUrl(iFactor); 
    }

    /// <summary>Deletes user extra factor. </summary>
    /// <param name="id">Factor ID.</param>
    [HttpDelete, Route("factors/{id}")]
    public void DeleteFactor(Guid id) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      var factor = login.ExtraFactors.FirstOrDefault(f => f.Id == id);
      OpContext.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", id);
      session.DeleteEntity(factor); 
      session.SaveChanges();
      LoginManager.CheckLoginFactorsSetupCompleted(login);
    }

    //Email/phone verification
    /// <summary>Tells server to send the PIN to verify the extra factor.</summary>
    /// <param name="factorId">Factor ID.</param>
    /// <returns>The token identifying the started process for email verification.</returns>
    [HttpPost, Route("factors/{factorId}/pin")]
    public async Task<string> SendPin(Guid factorId) {
      var session = OpenSession(); 
      var login = GetCurrentLogin(session);
      var factor = login.ExtraFactors.FirstOrDefault(f => f.Id == factorId);
      OpContext.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", factorId);
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var token = processService.GenerateProcessToken(); 
      var process = processService.StartProcess(login, LoginProcessType.FactorVerification, token);
      var pin = processService.GeneratePin(process, factor);
      await processService.SendPinAsync(process, factor);
      session.SaveChanges(); 
      return process.Token;
    }

    /// <summary>Submits the PIN received by user (in email) to verify it. </summary>
    /// <param name="factorId">Factor ID.</param>
    /// <param name="pin">The PIN value.</param>
    /// <returns>True if PIN value matches; otherwise, false.</returns>
    /// <remarks>This end point requires logged in user, so it can be used to verify the pin
    /// entered by the user manually (copied from email). There is another endpoing 
    /// (login/factors/verify-pin) that does not require logged-in user, so it can 
    /// be used to automatically submit the pin by redirecting from the URL 
    /// embedded in email. </remarks>
    [HttpPut, Route("factors/{factorid}/pin/{pin}")]
    public bool SubmitPin(Guid factorId, string pin) {
      OpContext.ThrowIfEmpty(pin, ClientFaultCodes.ValueMissing, "pin", "Pin value missing");
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      var factor = login.ExtraFactors.FirstOrDefault(f => f.Id == factorId);
      OpContext.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", "Factor not found, ID: {0}", factorId);
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var processes = processService.GetActiveConfirmationProcesses(factor, LoginProcessType.FactorVerification);
      OpContext.ThrowIf(processes.Count == 0, ClientFaultCodes.ObjectNotFound, "LoginProcess", "Confirmation process not found or expired.");
      try {
        foreach(var process in processes)
          if(processService.SubmitPin(process, pin))
            return true;
        return false;
      } finally {
        session.SaveChanges(); 
      }
    }

    /// <summary>Changes user password. </summary>
    /// <param name="changeInfo">Change information containing old and new passwords.</param>
    [HttpPut, Route("password")]
    public void ChangePassword([FromBody] PasswordChangeInfo changeInfo) {
      OpContext.WebContext.MarkConfidential();
      OpContext.ThrowIfNull(changeInfo, ClientFaultCodes.ContentMissing, "Password", "Password change info is missing.");
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      LoginManager.ChangePassword(login, changeInfo.OldPassword, changeInfo.NewPassword);
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
    [HttpPost, Route("device")]
    public DeviceInfo RegisterDevice([FromBody] DeviceInfo device) {
      return RegisterOrUpdateDevice(device); 
    }

    /// <summary>Updates user (client) device information. </summary>
    /// <param name="device">The device information.</param>
    /// <returns>The updated device information.</returns>
    [HttpPut, Route("device")]
    public DeviceInfo UpdateDevice([FromBody] DeviceInfo device) {
      return RegisterOrUpdateDevice(device);
    }

    private DeviceInfo RegisterOrUpdateDevice([FromBody] DeviceInfo device) {
      var session = OpenSession();
      var login = GetCurrentLogin(session);
      ITrustedDevice deviceEnt = null;
      if(!string.IsNullOrWhiteSpace(device.Token))
        deviceEnt = login.GetDevice(device.Token);
      if(deviceEnt == null)
        deviceEnt = LoginManager.RegisterTrustedDevice(login, device.Type, device.TrustLevel);
      else {
        deviceEnt.TrustLevel = device.TrustLevel;
      }
      session.SaveChanges();
      return new DeviceInfo() { Token = deviceEnt.Token, TrustLevel = deviceEnt.TrustLevel, Type = deviceEnt.Type };
    }

    [HttpDelete, Route("device/{token}")]
    public void DeleteDevice(string token) {
      var session = OpenSession(); 
      var login = GetCurrentLogin(session);
      var deviceEnt = login.GetDevice(token);
      if(deviceEnt == null)
        return;
      session.DeleteEntity(deviceEnt);
      session.SaveChanges(); 
    }


    private ILogin GetCurrentLogin(IEntitySession session) {
      var login = LoginManager.GetLogin(session);
      OpContext.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found for user: {0}.", OpContext.User.UserName);
      return login; 
    }

  }
}
