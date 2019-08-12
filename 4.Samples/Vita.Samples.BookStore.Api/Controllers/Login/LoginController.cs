using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Entities;
using Vita.Entities.Services; 
using Vita.Entities.Api;
using Microsoft.AspNetCore.Mvc;
using Vita.Modules.Login;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {

  // Handles Login/logout operations
  [Route("api/login")]
  public class LoginController : BaseApiController {

    public LoginController() {
    }
    /// <summary>Performs user login with user name and password.</summary>
    /// <param name="request">The request object.</param>
    /// <returns>An object containing login attempt result.</returns>
    [HttpPost, Route("")]
    public LoginResponse Login(LoginRequest request) {
      OpContext.ThrowIfNull(request, ClientFaultCodes.ContentMissing, "LoginRequest", "Content object missing in API request.");
      OpContext.WebContext.Flags |= WebCallFlags.Confidential;
      //Login using LoginService

      var loginService = OpContext.App.GetService<ILoginService>();
      var loginResult = loginService.Login(this.OpContext, request.UserName, request.Password, request.TenantId, request.DeviceToken);
      var login = loginResult.Login;
      switch(loginResult.Status) {
        case LoginAttemptStatus.PendingMultifactor:
          var processService = OpContext.App.GetService<ILoginProcessService>();
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
          var displayName = OpContext.App.GetUserDispalyName(loginResult.User);
          return new LoginResponse() {
            Status = LoginAttemptStatus.Success,
            UserName = login.UserName, UserDisplayName = displayName,
            UserId = login.UserId, AltUserId = login.AltUserId, LoginId = login.Id,
            PasswordExpiresDays = login.GetExpiresDays(), Actions = loginResult.Actions, LastLoggedInOn = loginResult.PreviousLoginOn,
            SessionId = loginResult.SessionId,
            AuthenticationToken = CreateAuthToken()
          };
      }//switch
    }

    private string CreateAuthToken() {
      var tokenCreator = OpContext.App.GetService<IAuthenticationTokenHandler>();
      var claims = OpContext.App.GetUserClaims(OpContext);
      var loginStt = OpContext.App.GetConfig<LoginModuleSettings>();
      var expires = OpContext.App.TimeService.UtcNow.Add(loginStt.LoginTokenExpiration);
      var token = tokenCreator.CreateToken(claims, expires);
      return token; 
    }

    /// <summary>Logs out the user. </summary>
    [HttpDelete, Route("")]
    public void Logout() {
      if(OpContext.User.Kind != UserKind.AuthenticatedUser)
        return;

      var loginService = OpContext.App.GetService<ILoginService>();
      loginService.Logout(this.OpContext);
    } // method

    //Duplicate of method in PasswordResetController - that's ok I think
    /// <summary>Returns login process identified by a token. </summary>
    /// <param name="token">Token identifying the process.</param>
    /// <returns>Login process object.</returns>
    /// <remarks>Login process is a server-side persistent object that tracks user progress through multi-step operations like password reset, multi-factor login, etc.</remarks>
    [HttpGet, Route("multifactor/process")]
    public LoginProcess GetMultiFactorProcess(string token) {
      var session = OpContext.OpenSession(); 
      var process = GetMutiFactorProcess(session, token, throwIfNotFound: false);
      return process.ToModel();
    }

    /// <summary>Requests the server to generate and send a pin to user (by email or SMS). </summary>
    /// <param name="pinRequest">Pin request information. 
    /// Should contain process token and factor type (email, phone).</param>
    [HttpPost, Route("multifactor/pin/send")]
    public async Task SendPinForMultiFactor(SendPinRequest pinRequest) {
      var session = OpContext.OpenSession();
      var process = GetMutiFactorProcess(session, pinRequest.ProcessToken);
      OpContext.ThrowIf(process.CurrentFactor != null, ClientFaultCodes.InvalidAction, "token", "Factor verification pending, the previous process step is not completed.");
      var pendingFactorTypes = process.PendingFactors;
      OpContext.ThrowIf(!pendingFactorTypes.IsSet(pinRequest.FactorType), ClientFaultCodes.InvalidValue, "factortype", "Factor type is not pending in login process");
      var factor = process.Login.ExtraFactors.FirstOrDefault(f => f.FactorType == pinRequest.FactorType); 
      OpContext.ThrowIfNull(factor, ClientFaultCodes.ObjectNotFound, "factor", 
        "Login factor (email or phone) not setup in user account; factor type: {0}", pinRequest.FactorType);
      var processService = OpContext.App.GetService<ILoginProcessService>();
      await processService.SendPinAsync(process, factor); 
    }

    /// <summary>Asks the server to verify the pin entered by the user or extracted from URL in email. </summary>
    /// <param name="request">The request object containing process token and pin value.</param>
    /// <returns>True if the pin is verified and is correct; otherwise, false.</returns>
    [HttpPut, Route("multifactor/pin/verify")]
    public bool VerifyPinForMultiFactor(VerifyPinRequest request) {
      var session = OpContext.OpenSession();
      var process = GetMutiFactorProcess(session, request.ProcessToken);
      OpContext.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "Process", "Process not found or expired.");
      OpContext.ThrowIfEmpty(request.Pin, ClientFaultCodes.ValueMissing, "Pin", "Pin value missing.");
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var result = processService.SubmitPin(process, request.Pin);
      session.SaveChanges();
      return result; 
    }

    /// <summary>Requests the server to complete the multi-factor login process and to actually login the user. </summary>
    /// <param name="request">The request data: process token and expiration type.</param>
    /// <returns>User login information.</returns>
    /// <remarks>The process must be completed, all factors specified should be verified by this time, 
    /// so PendingFactors property is empty.</remarks>
    [HttpPost, Route("multifactor/complete")]
    public LoginResponse CompleteMultiFactorLogin(MultifactorLoginRequest request) {
      var session = OpContext.OpenSession();
      var process = GetMutiFactorProcess(session, request.ProcessToken);
      OpContext.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "processToken", "Login process not found or expired.");
      OpContext.ThrowIf(process.PendingFactors != ExtraFactorTypes.None, ClientFaultCodes.InvalidValue, "PendingFactors",
        "Multi-factor login process not completed, verification pending: {0}.", process.PendingFactors);
      var login = process.Login;

      var loginService = OpContext.App.GetService<ILoginService>();
      var loginResult = loginService.CompleteMultiFactorLogin(OpContext, login);
      var displayName = OpContext.App.GetUserDispalyName(OpContext.User);
      session.SaveChanges(); 
      return new LoginResponse() {
        Status = LoginAttemptStatus.Success, SessionId = loginResult.SessionId,
        UserName = login.UserName, UserDisplayName = displayName,
        UserId = login.UserId, AltUserId = login.AltUserId, LoginId = login.Id,
        PasswordExpiresDays = login.GetExpiresDays(), Actions = loginResult.Actions,
        LastLoggedInOn = loginResult.PreviousLoginOn,
        AuthenticationToken = CreateAuthToken()
      };
    }//method

    /// <summary>Submits the PIN received by user (in email) to verify email. </summary>
    /// <param name="processToken">Verification process token.</param>
    /// <param name="pin">The PIN value.</param>
    /// <returns>True if PIN value matches; otherwise, false.</returns>
    /// <remarks>This method does not require logged-in user. Use it when from a page activated 
    /// by URL embedded in verification email. 
    /// The other end point for pin verification (api/mylogin/factors/pin)
    /// requires logged in user, so it should be used only when user enters the pin manually on the page.
    /// </remarks>
    [HttpPut, Route("factors/verify-pin")]
    public bool VerifyEmailPin(string processToken, string pin) {
      OpContext.ThrowIfEmpty(processToken, ClientFaultCodes.ValueMissing, "processToken", "ProcessToken value missing");
      OpContext.ThrowIfEmpty(pin, ClientFaultCodes.ValueMissing, "pin", "Pin value missing");
      var session = OpContext.OpenSession();
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var process = processService.GetActiveProcess(session, LoginProcessType.FactorVerification, processToken);
      OpContext.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "processToken", "Login process not found or expired.");
      var result = processService.SubmitPin(process, pin);
      session.SaveChanges();
      return result; 
    }

    /// <summary>Returns the list of user&quot;s secret questions. </summary>
    /// <param name="token">The token of login process.</param>
    /// <returns>The list of secret question objects for the user.</returns>
    [HttpGet, Route("multifactor/userquestions")]
    public IList<SecretQuestion> GetUserSecretQuestions(string token) {
      var session = OpContext.OpenSession();
      var process = GetMutiFactorProcess(session, token);
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var qs = processService.GetUserSecretQuestions(process.Login);
      return qs.Select(q => q.ToModel()).ToList();
    }

    /// <summary>Verifies the answers to the secret questions by comparing them to original answers stored in the user account. </summary>
    /// <param name="token">Login process token.</param>
    /// <param name="answers">The answers.</param>
    /// <returns>True if the answers are correct; otherwise, false.</returns>
    [HttpPut, Route("multifactor/questionanswers")]
    public bool SubmitQuestionAnswers(string token, List<SecretQuestionAnswer> answers) {
      OpContext.WebContext.MarkConfidential();
      var session = OpContext.OpenSession();
      var process = GetMutiFactorProcess(session, token);
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var result = processService.CheckAllSecretQuestionAnswers(process, answers);
      return result;
    }

    /// <summary>Verifies a single answer to the secret question. </summary>
    /// <param name="token">Login process token.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>True if the answer is correct; otherwise, false.</returns>
    [HttpPut, Route("multifactor/questionanswer")]
    public bool SubmitQuestionAnswer(string token, SecretQuestionAnswer answer) {
      OpContext.WebContext.MarkConfidential();
      var session = OpContext.OpenSession();
      var process = GetMutiFactorProcess(session, token);
      var storedAnswer = process.Login.SecretQuestionAnswers.FirstOrDefault(a => a.Question.Id == answer.QuestionId);
      OpContext.ThrowIfNull(storedAnswer, ClientFaultCodes.InvalidValue, "questionId", "Question is not registered user question.");
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var success = processService.CheckSecretQuestionAnswer(process, storedAnswer.Question, answer.Answer);
      return success;
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
    /// <summary>Asks server to evaluate the strength of a password. </summary>
    /// <param name="changeInfo">The password change information containing the password.</param>
    /// <returns>The strength of the password.</returns>
    [HttpPut, Route("passwordcheck")]
    public PasswordStrength EvaluatePasswordStrength(PasswordChangeInfo changeInfo) {
      OpContext.WebContext.MarkConfidential(); //prevent from logging password
      OpContext.ThrowIfNull(changeInfo, ClientFaultCodes.ContentMissing, "Password", "Password infomation missing.");
      var loginMgr =  OpContext.App.GetService<ILoginManagementService>();
      var strength = loginMgr.EvaluatePasswordStrength(changeInfo.NewPassword);
      return strength; 
    }

    private ILoginProcess GetMutiFactorProcess(IEntitySession session, string token, bool throwIfNotFound = true) {
      OpContext.ThrowIfEmpty(token, ClientFaultCodes.ValueMissing, "token", "Process token may not be empty.");
      var processService = OpContext.App.GetService<ILoginProcessService>();
      var process = processService.GetActiveProcess(session, LoginProcessType.MultiFactorLogin, token);
      if(throwIfNotFound)
        OpContext.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "LoginProcess", "Invalid login process token - process not found or expired.");
      return process; 
    }


  }

}
