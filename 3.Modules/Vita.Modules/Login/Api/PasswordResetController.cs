using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;
using Vita.Entities.Services;
using Vita.Modules.Logging;
using Vita.Modules.ApiClients.Recaptcha;

namespace Vita.Modules.Login.Api {

  // Handles Password reset
  [ApiRoutePrefix("passwordreset"), ApiGroup("Login/PasswordReset")] 
  public class PasswordResetController : SlimApiController {
    LoginModuleSettings _loginSettings; 
    ILoginProcessService _processService;
    IIncidentLogService _incidentLog;

    public override void InitController(OperationContext context) {
      base.InitController(context);
      _processService = context.App.GetService<ILoginProcessService>();
      _incidentLog = Context.App.GetService<IIncidentLogService>();
      _loginSettings = Context.App.GetConfig<LoginModuleSettings>();
    }


    [ApiPost, ApiRoute("")]
    public string Start(PasswordResetStartRequest request) {
      if(_loginSettings.Options.IsSet(LoginModuleOptions.RequireCaptchaOnPasswordReset))
        _processService.CheckCaptcha(Context, request.Captcha);
      var obscured = _loginSettings.Options.IsSet(LoginModuleOptions.ConcealMembership);
      var processToken = _processService.GenerateProcessToken(); 
      var emailFactor = _processService.FindLoginExtraFactor(Context, ExtraFactorTypes.Email, request.Factor);
      if(emailFactor == null) {
        // We may not indicate failure to the user, but we log incident anyway.
        LogIncident("StartFailed", "Failed attempt to start password reset process, email {0} not found.", request.Factor);
        // If we need to conceal membership (we are a public porn site), we have to pretend it went ok, and return processToken
        // so that we do not disclose if user's email is/is not in our database
        if(obscured)
          return processToken;
        else
          //if we are a specialized site and do not need to conceal membership (this might be annoying in a business system) -
          // we return error
          Context.ThrowIf(true, ClientFaultCodes.ObjectNotFound, "email", "Email {0} not found in database.", request.Factor);
      }
      //check we can start login
      var login = emailFactor.Login;
      if(login.Flags.IsSet(LoginFlags.DoNotConcealMembership))
        obscured = false; 

      bool accountBlocked = login.Flags.IsSet(LoginFlags.Disabled) || (login.Flags.IsSet(LoginFlags.Suspended) && !_loginSettings.Options.IsSet(LoginModuleOptions.AllowPasswordResetOnSuspended)); 
      if(accountBlocked) {
        if(obscured)
          return processToken;
        else
          Context.ThrowIf(true, LoginFaultCodes.LoginDisabled, "Login", "Login is disabled.");
      }
      //A flag in login entity may override default conceal settings - to allow more convenient disclosure for 
      // special members (stuff members or partners)
      var process = _processService.StartProcess(login, LoginProcessType.PasswordReset, processToken);
      return processToken; 
    }

    // Returns process ONLY if at least one factor was confirmed; otherwise hacker can figure out if user/email is in our database.
    // He starts password reset process using some email - StartProcess always returns process token, even if the process did not start. 
    // Hacker then tries to retrieve the process info using GetProcess. If we return a process object, this is an indicator that 
    // email is valid. To avoid this, this method requires that at least one factor (email or phone) is confirmed - meaning the user in fact 
    // controls the factor. Other methods behave in a similar way. 
    [ApiGet, ApiRoute("{token}")]
    public LoginProcess GetProcess(string token) {
      var process = GetActiveProcess(token);
      if(process == null)
        return null; 
      return process.ToModel();
    }


    [ApiPost, ApiRoute("{token}/pin")]
    public void SendPin(string token, SendPinRequest request) {
      Context.WebContext.MarkConfidential();
      Context.ThrowIfNull(request, ClientFaultCodes.ContentMissing, "SendPinRequest", "Pin request object must be provided.");
      Context.ValidateNotEmpty(token, "ProcessToken", "Process token should be provided.");
      Context.ValidateNotEmpty(request.Factor, "Factor", "Factor (email or phone) should be provided.");
      Context.ThrowValidation(); 
      var process = GetActiveProcess(token, confirmedOnly: false);
      if(process == null)
        return; //no indication process exist or not
      Context.ThrowIf(process.CurrentFactor != null, ClientFaultCodes.InvalidAction, "token", "The previous process step is not completed."); 
      var iFactor = _processService.FindLoginExtraFactor(process.Login, request.Factor);
      //now having completed at least one extra factor, we can openly indicate that we could not find next factor
      Context.ThrowIfNull(iFactor, ClientFaultCodes.InvalidValue, "factor", "Login factor (email or phone) is not found for a user.");
      //Check that factor type is one in the pending steps
      var factorOk = process.PendingFactors.IsSet(iFactor.FactorType);
      Context.ThrowIf(!factorOk, ClientFaultCodes.InvalidValue, "factor", "Login factor type attempted (email or phone) is not pending in the process.");
      _processService.SendPin(process, iFactor, request.Factor); //we use factor from request, to avoid unencrypting twice
    }

    [ApiPut, ApiRoute("{token}/pin/{pin}")]
    public void SubmitPin(string token, string pin) {
      var process = GetActiveProcess(token, confirmedOnly: false);
      Context.ThrowIfEmpty(pin, ClientFaultCodes.ValueMissing, "pin", "Pin value missing");
      if(process != null) 
        _processService.SubmitPin(process, pin);
    }

    // Should be used in 'this is not me!' URL in email to user, to abort the process and signal that this is an attack. 
    [ApiDelete, ApiRoute("{token}")]
    public void AbortProcess(string token) {
      var process = GetActiveProcess(token, confirmedOnly: false); 
      if(process != null) 
        _processService.AbortPasswordReset(process);
    }

    [ApiGet, ApiRoute("{token}/userquestions")]
    public IList<SecretQuestion> GetUserQuestions(string token) {
      var process = GetActiveProcess(token);
      if(process == null)
        return new List<SecretQuestion>();
      var qs = _processService.GetUserSecretQuestions(process.Login);
      var list = qs.Select(q => q.ToModel()).ToList();
      return list; 
    }

    [ApiPut, ApiRoute("{token}/questionanswer")]
    public bool SubmitSecretQuestionAnswer(string token, SecretQuestionAnswer answer) {
      Context.WebContext.MarkConfidential();
      var process = GetActiveProcess(token);
      if(process == null)
        return false;
      var storedAnswer = process.Login.SecretQuestionAnswers.FirstOrDefault(a => a.Question.Id == answer.QuestionId);
      Context.ThrowIfNull(storedAnswer, ClientFaultCodes.InvalidValue, "questionId", "Question is not registered user question.");
      var success = _processService.CheckSecretQuestionAnswer(process, storedAnswer.Question, answer.Answer); 
      return success; 
    }

    [ApiPut, ApiRoute("{token}/questionanswers")]
    public bool SubmitAllQuestionAnswers(string token, List<SecretQuestionAnswer> answers) {
      Context.WebContext.MarkConfidential();
      var process = GetActiveProcess(token);
      if(process == null)
        return false;
      var result = _processService.CheckAllSecretQuestionAnswers(process, answers);
      return result; 
    }

    [ApiPut, ApiRoute("{token}")]
    public bool ResetPassword(string token, PasswordChangeInfo changeInfo) {
      Context.WebContext.MarkConfidential();
      var process = GetActiveProcess(token);
      if(process == null)
        return false;
      _processService.ResetPassword(process, changeInfo.NewPassword);
      return true;
    }


    //Private utilities
    private ILoginProcess GetActiveProcess(string token, bool confirmedOnly = true) {
      var process = _processService.GetActiveProcess(Context, LoginProcessType.PasswordReset, token);
      if(process == null)
        return null;
      if(confirmedOnly && process.CompletedFactors == ExtraFactorTypes.None)
        return null;
      return process; 
    }

    private void LogIncident(string subType, string message, params object[] args) {
      if(_incidentLog == null)
        return;
      var msg = StringHelper.SafeFormat(message, args);
      _incidentLog.LogIncident("PasswordResetProcess", msg, subType, operationContext: Context);
    }



  }

}
