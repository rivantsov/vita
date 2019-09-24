using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Services;

// Implementation of IPasswordResetService - for processes like email confirmation, password reset, multifactor login
// See here for detailed discussion of password reset feature: 
// http://www.troyhunt.com/2012/05/everything-you-ever-wanted-to-know.html

namespace Vita.Modules.Login {

  public partial class LoginModule{

    public string GenerateProcessToken() {
      return RandomHelper.GenerateRandomString(10) + "-" + Guid.NewGuid();
    }

    public ILoginExtraFactor FindLoginExtraFactor(IEntitySession session, ExtraFactorTypes factorType, string factor) {
      var entFactor = session.EntitySet<ILoginExtraFactor>()
                     .FirstOrDefault(ef => ef.FactorType == factorType && ef.FactorValue == factor);
      VerifyExpirationSuspensionDates(entFactor.Login);
      return entFactor;
    }

    public ILoginExtraFactor FindLoginExtraFactor(ILogin login, string factor) {
      //We search by hash first, then decrypt and compare value
      VerifyExpirationSuspensionDates(login); 
      return login.ExtraFactors.FirstOrDefault(f => f.FactorValue == factor);
    }

    public ILoginProcess StartProcess(ILogin login, LoginProcessType processType, string token) {
      var session = EntityHelper.GetSession(login);
      //Figure out steps
      var pendingFactors = ExtraFactorTypes.None;
      switch(processType) {
        case LoginProcessType.PasswordReset:
          pendingFactors = login.PasswordResetFactors;
          if(pendingFactors == ExtraFactorTypes.None)
            pendingFactors = _settings.DefaultPasswordResetFactors; 
          OnLoginEvent(session.Context, LoginEventType.PasswordResetStarted, login);
          break;
        case LoginProcessType.MultiFactorLogin:
          pendingFactors = login.MultiFactorLoginFactors;
          if(pendingFactors == ExtraFactorTypes.None)
            pendingFactors = ExtraFactorTypes.Email; 
          OnLoginEvent(session.Context, LoginEventType.MultiFactorLoginStarted, login);
          break;
        case LoginProcessType.FactorVerification:
          //we do not specify steps for factor verification - whatever factor is started (with SendPin), we complete it
          OnLoginEvent(session.Context, LoginEventType.FactorVerificationStarted, login);
          break;
      }
      //For password reset process see if we need to set Obscured flag, indicating that we need to hide information
      // when returning info to the client - to avoid detecting if user's email exists in the database.  
      var processFlags = LoginProcessFlags.None;
      if(processType == LoginProcessType.PasswordReset) {
        // First check if we have global flag in settings, but flag in Login can override it.
        bool isObscured = _settings.Options.IsSet(LoginModuleOptions.ConcealMembership);
        // A flag in login can of
        if(login.Flags.IsSet(LoginFlags.DoNotConcealMembership))
          isObscured = false; 
        if (isObscured)
          processFlags |= LoginProcessFlags.Obscured;
      }
      //create an entity
      var process = session.NewEntity<ILoginProcess>();
      process.Login = login;
      process.ProcessType = processType;
      process.Token = token;
      process.PendingFactors = pendingFactors;
      process.Flags = processFlags; 
      var expPeriod = GetExpirationPeriod(processType);
      process.ExpiresOn = App.TimeService.UtcNow.Add(expPeriod);
      process.Status = LoginProcessStatus.Active; 
      session.SaveChanges();
      return process;
    }

    public TimeSpan GetExpirationPeriod(LoginProcessType processType) {
      switch(processType) {
        case LoginProcessType.PasswordReset:   return _settings.PasswordResetTimeWindow;
        case LoginProcessType.FactorVerification: return _settings.FactorVerificationTimeWindow;
        case LoginProcessType.MultiFactorLogin: 
        default: 
          return _settings.MultiFactorLoginTimeWindow;
      }
    }

    public string GeneratePin(ILoginProcess process, ILoginExtraFactor factor) {
      return _settings.PinGenerator(process, factor);
    }

    public async Task SendPinAsync(ILoginProcess process, ILoginExtraFactor factor, string factorValue = null, string pin = null) {
      Util.Check(process.Login == factor.Login, "LoginProcess tries to use login factor not assigned to process's login.");
      var session = EntityHelper.GetSession(process);
      var activeAlready = GetActiveConfirmationProcesses(factor, LoginProcessType.FactorVerification);
      session.Context.ThrowIf(activeAlready.Count > 2, ClientFaultCodes.InvalidAction, "Factor",
        "Cannot send pin - several pins already sent and are waiting for confirmation.");
      process.CurrentFactor = factor;
      if (factor.FactorType == ExtraFactorTypes.GoogleAuthenticator) {
        //do not generate or send pin
      } else {
        Util.Check(_settings.MessagingService != null, "Login messaging service not provided, cannot send pin.");
        pin = pin ?? _settings.PinGenerator(process, factor);
        if(string.IsNullOrWhiteSpace(factorValue))
          factorValue = factor.FactorValue;
        process.CurrentPin = pin;
        var logMsg = Util.SafeFormat("Pin sent to user {0} using factor '{1}'. ", factor.Login.UserName, factorValue);
        OnLoginEvent(session.Context, LoginEventType.PinSent, factor.Login, logMsg);
        await _settings.MessagingService.SendMessage(session.Context, LoginMessageType.Pin, factor, process);
      }
      session.SaveChanges();
    }

    //submit pin entered by user or from clicked URL
    public bool SubmitPin(ILoginProcess process, string pin) {
      var session = EntityHelper.GetSession(process);
      session.Context.ThrowIfEmpty(pin, ClientFaultCodes.InvalidAction, "Pin", "Pin may not be empty.");
      pin = pin.ToUpperInvariant(); 
      session.Context.ThrowIfEmpty(process.CurrentPin, ClientFaultCodes.InvalidAction, "Pin", "Process.Pin is not expected by the login process.");
      session.Context.ThrowIfNull(process.CurrentFactor, ClientFaultCodes.InvalidAction, "Pin", "Current factor is not set in login process.");
      bool match;
      if (process.CurrentFactor.FactorType == ExtraFactorTypes.GoogleAuthenticator) {
        var secret = process.CurrentFactor.FactorValue;
        match = GoogleAuthenticator.GoogleAuthenticatorUtil.CheckPasscode(secret, pin);
      } else {
        match = process.CurrentPin == pin;
      }
      if(!match) {
        process.FailCount++;
        OnLoginEvent(session.Context, LoginEventType.PinMismatch, process.Login, "Invalid pin: " + pin);
        return false;
      }
      // Pins match; we allow repeated submits of the same pin; we nullify CurrentFactor on first successful submit, 
      // but keep CurrentPin for future matches
      if(process.CurrentFactor != null) {
        OnLoginEvent(session.Context, LoginEventType.PinMatched, process.Login, "Pin matched, factor verified: " + process.CurrentFactor.FactorType);
        process.PendingFactors &= ~process.CurrentFactor.FactorType;
        process.CompletedFactors |= process.CurrentFactor.FactorType;
        if(process.ProcessType == LoginProcessType.FactorVerification) {
          process.CurrentFactor.VerifiedOn = App.TimeService.UtcNow;
          process.Status = LoginProcessStatus.Completed;
        }
        process.CurrentFactor = null; //nullify factor but keep pin
      }
      session.SaveChanges();
      if(process.ProcessType == LoginProcessType.FactorVerification)
        CheckLoginFactorsSetupCompleted(process.Login);
      return true; 
    }

    public ILoginProcess GetActiveProcess(IEntitySession session, LoginProcessType processType, string token) {
      Util.Check(!string.IsNullOrWhiteSpace(token), "Process token may not be null");
      var context = session.Context;
      //get process without expiration checking, we check it later
      var query = from p in session.EntitySet<ILoginProcess>()
                  where p.ProcessType == processType && p.Token == token 
                  select p;
      var process = query.FirstOrDefault();
      context.ThrowIfNull(process, ClientFaultCodes.ObjectNotFound, "Process", "Login process not found.");
      if(process.Status != LoginProcessStatus.Active)
        return null;
      if (process.FailCount >= _settings.MaxFailCount) {
        process.Status = LoginProcessStatus.AbortedAsFraud;
        OnLoginEvent(context, LoginEventType.ProcessAborted, process.Login, "Aborted - too many failures.");
        session.SaveChanges();
        return null;
      }
      var userName = process.Login.UserName; 
      if(process.ExpiresOn < App.TimeService.UtcNow) {
        process.Status = LoginProcessStatus.Expired;
        OnLoginEvent(context, LoginEventType.ProcessAborted, process.Login, "Expired.");
        session.SaveChanges();
        return null;
      }
      return process; 
    }

    //to account for multiple pins sent (user might be tired of waiting for first pin, click again and make another pin sent)
    public IList<ILoginProcess> GetActiveConfirmationProcesses(ILoginExtraFactor factor, LoginProcessType processType) {
      var session = EntityHelper.GetSession(factor);
      var utcNow = App.TimeService.UtcNow; 
      var query = from p in session.EntitySet<ILoginProcess>()
                  where p.ExpiresOn > utcNow && p.ProcessType == processType && p.CurrentFactor == factor &&
                        p.Status == LoginProcessStatus.Active && p.FailCount < _settings.MaxFailCount
                  orderby p.CreatedOn descending
                  select p;
      var processes = query.ToList();
      return processes; 
    }

    //TODO: block the IP of the originator of the process after multiple failures, build IncidentTrigger for this
    public void AbortPasswordReset(ILoginProcess process) {
      var session = EntityHelper.GetSession(process);
      if(process.Status != LoginProcessStatus.Active)
        return; 
      process.Status = LoginProcessStatus.AbortedAsFraud;
      OnLoginEvent(session.Context, LoginEventType.ProcessAborted, process.Login, "Login process aborted");
      session.SaveChanges(); 
    }
    
    // Check secret questions
    public IList<ISecretQuestion> GetUserSecretQuestionsx(ILogin login) {
      return login.SecretQuestionAnswers.Select(qa => qa.Question).ToList();
    }
    
    public bool CheckSecretQuestionAnswer(ILoginProcess process, ISecretQuestion question, string userAnswer) {
      var session = EntityHelper.GetSession(process); 
      var qa = process.Login.SecretQuestionAnswers.FirstOrDefault(a => a.Question == question);
      session.Context.ThrowIfNull(qa, ClientFaultCodes.InvalidValue, "question", 
        "The question is not registered as user question. Question: {0}", question.Question);
      var match = CheckUserAnswer(qa, userAnswer);
      if(!match) {
        process.FailCount++;
        session.SaveChanges(); 
        OnLoginEvent(session.Context, LoginEventType.QuestionAnswersFailed, process.Login, "Secret questions check failed.");
        return false; 
      }
      // Add question number to list of answered questions in process record. 
      // If all questions are answered, clear Pending step CheckQuestionAnswers
      var strAllAnswered = process.AnsweredQuestions ?? string.Empty; 
      var answeredNumbers = strAllAnswered.Split(',').Select(sn => sn.Trim()).ToList(); 
      var newAnsNum = qa.Number.ToString();
      if(!answeredNumbers.Contains(newAnsNum)) {
        answeredNumbers.Add(newAnsNum);
        process.AnsweredQuestions = string.Join(",", answeredNumbers);
        if(answeredNumbers.Count == process.Login.SecretQuestionAnswers.Count) {
          //Set questions as answers
          process.PendingFactors &= ~ExtraFactorTypes.SecretQuestions; 
        }
        session.SaveChanges(); 
      }
      return true; 
    }

    public bool CheckAllSecretQuestionAnswers(ILoginProcess process, IList<SecretQuestionAnswer> answers) {
      var session = EntityHelper.GetSession(process); 
      var context = session.Context;
      bool result = true; 
      foreach(var qa in process.Login.SecretQuestionAnswers) {
        var ans = answers.FirstOrDefault(a => a.QuestionId == qa.Question.Id);
        if(ans == null) 
          result = false ; //question not answered 
        else 
          result &= CheckUserAnswer(qa, ans.Answer);
      }
      if(!result) {
        process.FailCount++;
        session.SaveChanges(); 
        OnLoginEvent(context, LoginEventType.QuestionAnswersFailed, process.Login, "Secret questions check failed.");
        return false; 
      }
      // Success
      // Save numbers of answered questions, clear flag in pending steps
      process.AnsweredQuestions = string.Join(",", process.Login.SecretQuestionAnswers.Select(qa => qa.Number));
      process.PendingFactors &= ~ExtraFactorTypes.SecretQuestions;
      session.SaveChanges();
      OnLoginEvent(context, LoginEventType.QuestionAnswersSucceeded, process.Login, "Secret questions check succeeded.");
      return result; 
    }

    public async Task ResetPasswordAsync(ILoginProcess process, string newPassword) {
      var session = EntityHelper.GetSession(process);
      var ctx = session.Context; 
      ctx.ThrowIf(process.Status != LoginProcessStatus.Active, ClientFaultCodes.InvalidAction, "ProcessStatus", "Process is not active.");
      if(process.ExpiresOn < App.TimeService.UtcNow) {
        process.Status = LoginProcessStatus.Expired;
        session.SaveChanges(); 
        ctx.ThrowIf(true, ClientFaultCodes.InvalidAction, "Process", "Process expired.");
      }
      ctx.ThrowIf(process.ProcessType != LoginProcessType.PasswordReset, ClientFaultCodes.InvalidValue, "ProcessType", "Invalid login process type.");
      ctx.ThrowIf(process.PendingFactors != ExtraFactorTypes.None, ClientFaultCodes.InvalidAction, "PendingSteps", "The login process has verification steps pending");
      //Ok, we're ready 
      var login = process.Login; 
      ChangeUserPassword(login, newPassword, oneTimeByAdmin: false); //will log incident and fire event 
      login.Flags &= ~LoginFlags.Suspended; //clear Suspended flag
      //complete process
      process.Status = LoginProcessStatus.Completed;
      session.SaveChanges();
      //Send notification
      var factor = FindLoginFactor(login, ExtraFactorTypes.Email);
      process.CurrentFactor = factor;
      session.SaveChanges(); 
      if(factor != null && _settings.MessagingService != null)
        await _settings.MessagingService.SendMessage(ctx, LoginMessageType.PasswordResetCompleted, factor, process);
    }

    private bool CheckUserAnswer(ISecretQuestionAnswer storedAnswer, string userAnswer) {
      var ahash = GetWeakSecretAnswerHash(userAnswer, storedAnswer.Login.Id);
      return ahash == storedAnswer.AnswerHash;
    }

    //This metod is a default pin generator and is referenced by LoginModuleSettings.PinGenerator
    public static string DefaultGeneratePin(ILoginProcess process, ILoginExtraFactor factor) {
      switch(factor.FactorType) {
        case ExtraFactorTypes.Phone:
          return RandomHelper.GenerateSafeRandomNumber(5);
        case ExtraFactorTypes.Email:
        default:
          return RandomHelper.GenerateSafeRandomWord(10);
      }
    }

  }//module

}//ns
