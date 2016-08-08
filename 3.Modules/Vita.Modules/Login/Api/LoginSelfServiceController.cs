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

    [ApiGet, ApiRoute("")]
    public LoginInfo GetLoginInfo() {
      var login = GetCurrentLogin();
      return login.ToModel(); 
    }

    [ApiPut, ApiRoute("")]
    public LoginInfo UpdateLoginInfo(LoginInfo loginInfo) {
      var login = GetCurrentLogin();
      Context.ThrowIfNull(login, ClientFaultCodes.ContentMissing, "LoginInfo", "LoginInfo is missing.");
      Context.ThrowIf(loginInfo.Id != login.Id, ClientFaultCodes.InvalidValue, "Id", "Invalid login Id, must match current user's login id.");
      _loginManager.UpdateLogin(login, loginInfo);
      return login.ToModel(); 
    }

    [ApiGet, ApiRoute("allquestions")]
    public List<SecretQuestion> GetStandardSecretQuestions() {
      var session = Context.OpenSystemSession(); //opening system session, no need for authorization
      var questions = _loginManager.GetAllSecretQuestions(session);
      return questions.Select(q => q.ToModel()).ToList();
    }

    [ApiGet, ApiRoute("questions")]
    public List<SecretQuestion> GetMySecretQuestions() {
      var login = GetCurrentLogin();
      var questions = _loginManager.GetUserSecretQuestions(login);
      return questions.Select(q => q.ToModel()).ToList();
    }
    [ApiPut, ApiRoute("answers")]
    public void SetUserSecretQuestionAnswers(List<SecretQuestionAnswer> answers) {
      var login = GetCurrentLogin();
      _loginManager.UpdateUserQuestionAnswers(login, answers);
      _loginManager.CheckLoginFactorsSetupCompleted(login);
    }

    [ApiPut, ApiRoute("answers/order")]
    public void ReorderUserQuestionAnswers(IList<Guid> ids) {
      var login = GetCurrentLogin();
      _loginManager.ReorderUserQuestionAnswers(login, ids);
    }

    [ApiGet, ApiRoute("factors")]
    public IList<LoginExtraFactor> GetUserFactors() {
      var login = GetCurrentLogin();
      return _loginManager.GetUserFactors(login); 
    }

    [ApiGet, ApiRoute("factors/{id}")]
    public LoginExtraFactor GetUserFactor(Guid factorId) {
      var login = GetCurrentLogin();
      return _loginManager.GetUserFactor(login, factorId);
    }


    //For GoogleAutenticator, value is ignored and secret is generated
    [ApiPost, ApiRoute("factors")]
    public LoginExtraFactor AddUserFactor(LoginExtraFactor factor) {
      var login = GetCurrentLogin();
      var newFactor = _loginManager.AddFactor(login, factor.Type, factor.Value);
      var session = EntityHelper.GetSession(login);
      session.SaveChanges();
      _loginManager.CheckLoginFactorsSetupCompleted(login); 
      return newFactor; 
    }

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
    // and this crushes the API Client generator (AutoRest)
    [ApiPost, ApiRoute("device")]
    public DeviceInfo RegisterDevice(DeviceInfo device) {
      return RegisterOrUpdateDevice(device); 
    }
    [ApiPut, ApiRoute("device")]
    public DeviceInfo UpdateDevice(DeviceInfo device) {
      return RegisterOrUpdateDevice(device);
    }

    private DeviceInfo RegisterOrUpdateDevice(DeviceInfo device) {
      var login = GetCurrentLogin();
      ITrustedDevice deviceEnt = null;
      if(!string.IsNullOrWhiteSpace(device.Token))
        deviceEnt = login.GetDevice(device.Token);
      if(deviceEnt == null)
        deviceEnt = _loginManager.RegisterTrustedDevice(login, device.Type, device.TrustLevel);
      else {
        deviceEnt.TrustLevel = device.TrustLevel;
        var session = EntityHelper.GetSession(login);
        session.SaveChanges(); 
      }
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
      Context.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found for user: {0}.", Context.User);
      return login; 
    }

    private IEntitySession OpenSession() {
      //TODO: maybe change to secure session; so far not necessary, everything in this controller happens in connection to currently logged in user.
      return Context.OpenSession(); 
    }
  }
}
