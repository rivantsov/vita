using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Modules.Logging;

namespace Vita.Modules.Login.Api {

  // Handles Login/logout operations
  [ApiRoutePrefix("login")]
  public class LoginController : SlimApiController {
    ILoginService _loginService;
    ILoginProcessService _processService;
    IUserSessionService _sessionService;

    public override void InitController(OperationContext context) {
      base.InitController(context);
      _loginService = Context.App.GetService<ILoginService>();
      _processService = Context.App.GetService<ILoginProcessService>();
      _sessionService = Context.App.GetService<IUserSessionService>(); 
    }

    /// <summary>Performs user login with user name and password.</summary>
    /// <param name="request">The request object.</param>
    /// <returns>An object containing login attempt result.</returns>
    [ApiPost, ApiRoute("")]
    public LoginResponse Login(LoginRequest request) {
      Context.ThrowIfNull(request, ClientFaultCodes.ContentMissing, "LoginRequest", "Content object missing in API request.");
      Context.WebContext.Flags |= WebCallFlags.Confidential;
      //Login using LoginService
      var loginResult = _loginService.Login(this.Context, request.UserName, request.Password, request.TenantId, request.DeviceToken);
      var login = loginResult.Login;
      switch(loginResult.Status) {
        case LoginAttemptStatus.PendingMultifactor:
          var processService = Context.App.GetService<ILoginProcessService>();
          var token = processService.GenerateProcessToken();
          var process = processService.StartProcess(loginResult.Login, LoginProcessType.MultiFactorLogin, token);
          return new LoginResponse() { Status = LoginAttemptStatus.PendingMultifactor, MultiFactorProcessToken = token };
        case LoginAttemptStatus.AccountInactive:
          // return AccountInactive only if login allows to disclose membership
          var reportStatus = login.Flags.IsSet(LoginFlags.DoNotConcealMembership) ? LoginAttemptStatus.AccountInactive : LoginAttemptStatus.Failed;
          return new LoginResponse() { Status = reportStatus };
        case LoginAttemptStatus.Failed:
        default:
          return new LoginResponse() { Status = loginResult.Status };
        case LoginAttemptStatus.Success:
          var displayName = Context.App.GetUserDispalyName(loginResult.User);
          return new LoginResponse() {
            Status = LoginAttemptStatus.Success, AuthenticationToken = loginResult.SessionToken, 
            UserName = login.UserName,  UserDisplayName = displayName,
            UserId = login.UserId, AltUserId = login.AltUserId, LoginId = login.Id,
            PasswordExpiresDays = login.GetExpiresDays(), Actions = loginResult.Actions, LastLoggedInOn = loginResult.LastLoggedInOn
          };
      }//switch
    }


    [ApiDelete, ApiRoute("")]
    public void Logout() {
      if(Context.User.Kind != UserKind.AuthenticatedUser)
        return; 
      _loginService.Logout(this.Context);
    } // method

    //Duplicate of method in PasswordResetController - that's ok I think
    [ApiGet, ApiRoute("{token}")]
    public LoginProcess GetMultiFactorProcess(string token) {
      var process = GetActiveProcess(token, throwIfNotFound: false);
      return process.ToModel();
    }

    [ApiPost, ApiRoute("{token}/pin")]
    public void SendPinForMultiFactor(string token, ExtraFactorTypes factorType) {
      var process = GetActiveProcess(token);
      Context.ThrowIf(process.CurrentFactor != null, ClientFaultCodes.InvalidAction, "token", "Factor verification pending, the previous process step is not completed.");
      var pendingFactorTypes = process.PendingFactors;
      Context.ThrowIf(!pendingFactorTypes.IsSet(factorType), ClientFaultCodes.InvalidValue, "factortype", "Factor type is not pending in login process");
      var factor = process.Login.ExtraFactors.FirstOrDefault(f => f.FactorType == factorType); 
      Context.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", 
        "Login factor (email or phone) not setup in user account; factor type: {0}", factorType);
      _processService.SendPin(process, factor); 
    }

    [ApiPut, ApiRoute("{token}/pin/{pin}")]
    public bool SubmitPinForMultiFactor(string token, string pin) {
      var process = GetActiveProcess(token);
      Context.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "Process", "Process not found or expired.");
      Context.ThrowIfEmpty(pin, ClientFaultCodes.ValueMissing, "Pin", "Pin value missing.");
      return _processService.SubmitPin(process, pin);
    }

    [ApiPost, ApiRoute("{token}")]
    public LoginResponse CompleteMultiFactorLogin(string token) {
      var process = GetActiveProcess(token);
      Context.ThrowIf(process.PendingFactors != ExtraFactorTypes.None, ClientFaultCodes.InvalidValue, "PendingFactors",
        "Multi-factor login process not completed, verification pending: {0}.", process.PendingFactors);
      var login = process.Login;
      var loginResult = _loginService.CompleteMultiFactorLogin(Context, login);
      var displayName = Context.App.GetUserDispalyName(Context.User);
      return new LoginResponse() {
        Status = LoginAttemptStatus.Success, AuthenticationToken = loginResult.SessionToken,
        UserName = login.UserName, UserDisplayName = displayName,
        UserId = login.UserId, AltUserId = login.AltUserId, LoginId = login.Id,
        PasswordExpiresDays = login.GetExpiresDays(), Actions = loginResult.Actions, LastLoggedInOn = loginResult.LastLoggedInOn
      };
    }//method

    [ApiGet, ApiRoute("{token}/userquestions")]
    public IList<SecretQuestion> GetUserSecretQuestions(string token) {
      var process = GetActiveProcess(token);
      var qs = _processService.GetUserSecretQuestions(process.Login);
      return qs.Select(q => q.ToModel()).ToList();
    }

    [ApiPut, ApiRoute("{token}/questionanswer")]
    public bool SubmitUserQuestionAnswer(string token, SecretQuestionAnswer answer) {
      Context.WebContext.MarkConfidential();
      var process = GetActiveProcess(token);
      var storedAnswer = process.Login.SecretQuestionAnswers.FirstOrDefault(a => a.Question.Id == answer.QuestionId);
      Context.ThrowIfNull(storedAnswer, ClientFaultCodes.InvalidValue, "questionId", "Question is not registered user question.");
      var success = _processService.CheckSecretQuestionAnswer(process, storedAnswer.Question, answer.Answer);
      return success;
    }

    [ApiPut, ApiRoute("{token}/questionanswers")]
    public bool SubmitAllUserQuestionAnswers(string token, List<SecretQuestionAnswer> answers) {
      Context.WebContext.MarkConfidential();
      var process = GetActiveProcess(token);
      var result = _processService.CheckAllSecretQuestionAnswers(process, answers);
      return result;
    }

    /* Notes: 
     * 1. GET verb would be more appropriate, but then password will appear in URL (GET does not allow body), 
         and we want to avoid this, so it does not appear in web log. With PUT we set HasSensitiveData flag and 
         this prevents web log from logging request body
       2. This controller is not a logical place to host this method, password checking will be used in UI for 
          self-service password change or password reset process. To avoid implementing it in several places,
          and considering that password check is light-weight (no db access) and does not need to be secured, 
          we place it here. 
     */ 
    [ApiPut, ApiRoute("passwordcheck")]
    public PasswordStrength EvaluatePasswordStrength(PasswordChangeInfo changeInfo) {
      Context.WebContext.MarkConfidential(); //prevent from logging password
      Context.ThrowIfNull(changeInfo, ClientFaultCodes.ContentMissing, "Password", "Password infomation missing.");
      var loginMgr =  Context.App.GetService<ILoginManagementService>();
      var strength = loginMgr.EvaluatePasswordStrength(changeInfo.NewPassword);
      return strength; 
    }

    // Note: have to use double-segment URL, othewise it is confused with "{token}" URL
    [ApiGet, ApiRoute("session/info")]
    public SessionInfo GetSessionInfo() {
      var session = Context.UserSession;
      if (session == null)
        return null;
      var user = Context.User; 
      var info = new SessionInfo() {UserName = user.UserName, Kind = user.Kind, UserId = user.UserId, Culture = Context.UserCulture, 
           StartedOn = session.StartedOn, TimeZoneOffsetMinutes = session.TimeZoneOffsetMinutes};
      return info; 
    }

    [ApiPut, ApiRoute("session/timezoneoffset")]
    public void SetTimezoneOffset(int minutes) {
      var userSession = Context.UserSession;
      if (userSession != null) {
        userSession.TimeZoneOffsetMinutes = minutes; //this will mark session as dirty, and it will be saved 
      }
    }

    private ILoginProcess GetActiveProcess(string token, bool throwIfNotFound = true) {
      Context.ThrowIfEmpty(token, ClientFaultCodes.ValueMissing, "token", "Process token may not be empty.");
      var process = _processService.GetActiveProcess(Context, LoginProcessType.MultiFactorLogin, token);
      if(throwIfNotFound)
        Context.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "LoginProcess", "Login process not found for token.");
      return process; 
    }


  }

}
