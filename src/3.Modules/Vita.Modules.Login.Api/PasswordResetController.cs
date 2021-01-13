﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Vita.Entities;
using Vita.Entities.Api;
using Vita.Web;

namespace Vita.Modules.Login.Api {

  // Handles Password reset
  [Route("api/passwordreset")] 
  public class PasswordResetController : BaseApiController {


    ILoginProcessService ProcessService {
      get {
        _processService = _processService ?? OpContext.App.GetService<ILoginProcessService>();
        return _processService; 
      }
    } ILoginProcessService _processService;

    /// <summary>Starts password reset process. </summary>
    /// <param name="request">The data object with Captcha value (if used) and authentication factor (email).</param>
    /// <returns>Process token identifying the started process. </returns>
    [HttpPost, Route("start")]
    public string Start([FromBody] PasswordResetStartRequest request) {
      var loginStt = OpContext.App.GetConfig<LoginModuleSettings>();
      var obscured = loginStt.Options.IsSet(LoginModuleOptions.ConcealMembership);
      var processToken = ProcessService.GenerateProcessToken(); 
      var session = OpContext.OpenSession(); 
      var emailFactor = ProcessService.FindLoginExtraFactor(session, ExtraFactorTypes.Email, request.Factor);
      if(emailFactor == null) {
        // If we need to conceal membership (we are a public porn site), we have to pretend it went ok, and return processToken
        // so that we do not disclose if user's email is/is not in our database
        if(obscured)
          return processToken;
        else
          //if we are a specialized site and do not need to conceal membership (this might be annoying in a business system) -
          // we return error
          OpContext.ThrowIf(true, ClientFaultCodes.ObjectNotFound, "email", "Email {0} not found in database.", request.Factor);
      }
      //check we can start login
      var login = emailFactor.Login;
      if(login.Flags.IsSet(LoginFlags.DoNotConcealMembership))
        obscured = false;

      bool accountBlocked = login.Flags.IsSet(LoginFlags.Disabled) || (login.Flags.IsSet(LoginFlags.Suspended) && 
            !loginStt.Options.IsSet(LoginModuleOptions.AllowPasswordResetOnSuspended)); 
      if(accountBlocked) {
        if(obscured)
          return processToken;
        else
          OpContext.ThrowIf(true, LoginFaultCodes.LoginDisabled, "Login", "Login is disabled.");
      }
      //A flag in login entity may override default conceal settings - to allow more convenient disclosure for 
      // special members (stuff members or partners)
      var process = ProcessService.StartProcess(login, LoginProcessType.PasswordReset, processToken);
      return processToken; 
    }

    // Returns process ONLY if at least one factor was confirmed; otherwise hacker can figure out if user/email is in our database.
    // He starts password reset process using some email - StartProcess always returns process token, even if the process did not start. 
    // Hacker then tries to retrieve the process info using GetProcess. If we return a process object, this is an indicator that 
    // email is valid. To avoid this, this method requires that at least one factor (email or phone) is confirmed - meaning the user in fact 
    // controls the factor. Other methods behave in a similar way. 

    /// <summary>Returns the information about password reset (login) process in progress.</summary>
    /// <param name="token">Process token.</param>
    /// <returns>Process information object. Client </returns>
    /// <remarks>Client can use this method to inquire list of extra factors that must be confirmed before password 
    /// is actually changed. Returns null if the process just started and no factors (email pins) were confirmed.
    /// This is a protection from disclosing membership to random users.
    /// User must confirm at least one factor (access to email) to get information using this method.</remarks>
    [HttpGet, Route("process")]
    public LoginProcess GetProcess(string token) {
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, token);
      if(process == null)
        return null; 
      return process.ToModel();
    }


    /// <summary>Sends secret pin through specified channel (email or phone) to confirm access to target inbox or phone. </summary>
    /// <param name="request">Parameters object.</param>
    /// <remarks>Factor parameter (email itself) should be provided in input object. FactorType is ignored.</remarks>
    [HttpPost, Route("pin/send")]
    public async Task SendPin([FromBody] SendPinRequest request) {
      OpContext.WebContext.MarkConfidential();
      OpContext.ThrowIfNull(request, ClientFaultCodes.ContentMissing, "SendPinRequest", "Pin request object must be provided.");
      OpContext.ValidateNotEmpty(request.ProcessToken, "ProcessToken", "Process token should be provided.");
      OpContext.ValidateNotEmpty(request.Factor, "Factor", "Factor (email or phone) should be provided.");
      OpContext.ThrowValidation();
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, request.ProcessToken, confirmedOnly: false);
      if(process == null)
        return; //no indication process exist or not
      OpContext.ThrowIf(process.CurrentFactor != null, ClientFaultCodes.InvalidAction, "token", "The previous process step is not completed."); 
      var iFactor = ProcessService.FindLoginExtraFactor(process.Login, request.Factor);
      //now having completed at least one extra factor, we can openly indicate that we could not find next factor
      OpContext.ThrowIfNull(iFactor, ClientFaultCodes.InvalidValue, "factor", "Login factor (email or phone) is not found for a user.");
      //Check that factor type is one in the pending steps
      var factorOk = process.PendingFactors.IsSet(iFactor.FactorType);
      OpContext.ThrowIf(!factorOk, ClientFaultCodes.InvalidValue, "factor", "Login factor type attempted (email or phone) is not pending in the process.");
      await ProcessService.SendPinAsync(process, iFactor, request.Factor); //we use factor from request, to avoid unencrypting twice
    }

    /// <summary>Verifies pin received by user in email or SMS. </summary>
    /// <param name="request">Pin data.</param>
    [HttpPut, Route("pin/verify")]
    public void VerifyPin([FromBody] VerifyPinRequest request) {
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, request.ProcessToken, confirmedOnly: false);
      OpContext.ThrowIfEmpty(request.Pin, ClientFaultCodes.ValueMissing, "pin", "Pin value missing");
      if(process != null) 
        ProcessService.SubmitPin(process, request.Pin);
    }

    /// <summary>Aborts the password reset process.</summary>
    /// <param name="token">Process token.</param>
    /// <remarks>Should be linked to negative response/link in email with PIN sent to the user (No, it is not me!) 
    /// - to abort the process.</remarks>
    [HttpDelete, Route("abort")]
    public void AbortProcess(string token) {
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, token, confirmedOnly: false); 
      if(process != null) 
        ProcessService.AbortPasswordReset(process);
    }

    /// <summary>Returns a list of secret questions for a user. </summary>
    /// <param name="token">Process token.</param>
    /// <returns>A list of secret questions previously setup by the user.</returns>
    [HttpGet, Route("userquestions")]
    public IList<SecretQuestion> GetUserQuestions(string token) {
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, token);
      if(process == null)
        return new List<SecretQuestion>();
      var qs = ProcessService.GetUserSecretQuestions(process.Login);
      var list = qs.Select(q => q.ToModel()).ToList();
      return list; 
    }

    /// <summary>Submits an answer to one secret question from the user. </summary>
    /// <param name="token">Process token.</param>
    /// <param name="answer">An object containing question ID and the answer. </param>
    /// <returns>True if the answer is correct; otherwise, false.</returns>
    [HttpPut, Route("userquestions/answer")]
    public bool SubmitSecretQuestionAnswer([FromQuery] string token, [FromBody] SecretQuestionAnswer answer) {
      OpContext.WebContext.MarkConfidential();
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, token);
      if(process == null)
        return false;
      var storedAnswer = process.Login.SecretQuestionAnswers.FirstOrDefault(a => a.Question.Id == answer.QuestionId);
      OpContext.ThrowIfNull(storedAnswer, ClientFaultCodes.InvalidValue, "questionId", "Question is not registered user question.");
      var success = ProcessService.CheckSecretQuestionAnswer(process, storedAnswer.Question, answer.Answer); 
      return success; 
    }

    /// <summary>Submits answers to secret questions from the user. </summary>
    /// <param name="token">Process token.</param>
    /// <param name="answers">A list of answer objects with question IDs and answers.</param>
    /// <returns>True if answers are correct; otherwise, false.</returns>
    [HttpPut, Route("userquestions/answers")]
    public bool SubmitAllQuestionAnswers(string token, [FromBody] SecretQuestionAnswer[] answers) {
      OpContext.WebContext.MarkConfidential();
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, token);
      if(process == null)
        return false;
      var result = ProcessService.CheckAllSecretQuestionAnswers(process, answers);
      return result; 
    }

    /// <summary>Sets new password. </summary>
    /// <param name="token">Process token.</param>
    /// <param name="changeInfo">An object containing new password.</param>
    /// <returns>True if password was successfully changed; otherwise, false.</returns>
    [HttpPut, Route("new")]
    public async Task<bool> SetNewPassword(string token, [FromBody] PasswordChangeInfo changeInfo) {
      OpContext.WebContext.MarkConfidential();
      var session = OpContext.OpenSession();
      var process = GetActiveProcess(session, token);
      if(process == null)
        return false;
      await ProcessService.ResetPasswordAsync(process, changeInfo.NewPassword);
      return true;
    }

    //Private utilities
    private ILoginProcess GetActiveProcess(IEntitySession session, string token, bool confirmedOnly = true) {
      var process = ProcessService.GetActiveProcess(session, LoginProcessType.PasswordReset, token);
      if(process == null)
        return null;
      if(confirmedOnly && process.CompletedFactors == ExtraFactorTypes.None)
        return null;
      return process; 
    }

  }//class
} //ns
