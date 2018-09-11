using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Vita.Entities;
using Vita.Entities.Api;
using Vita.Entities.Runtime;
using Vita.Entities.Services;

namespace Vita.Modules.Login {

  public partial class LoginModule {

    #region ILoginService Members
    public event EventHandler<LoginEventArgs> LoginEvent;

    public LoginResult Login(OperationContext context, string userName, string password, Guid? tenantId = null,
                             string deviceToken = null) {
      context.ThrowIf(password.Length > 100, ClientFaultCodes.InvalidValue, "password", "Password too long, max size: 100.");
      userName = CheckUserName(context, userName);
      var session = context.OpenSession();
      // find login and verify password
      try {
        var login = FindLogin(session, userName, password, tenantId);
        if(login == null || !VerifyPassword(login, password)) {
          if(context.WebContext != null)
            context.WebContext.Flags |= WebCallFlags.AttackRedFlag;
          if(login != null)
            OnLoginFailed(login);
          OnLoginEvent(context, LoginEventType.LoginFailed, null, userName: userName);
          return new LoginResult() { Status = LoginAttemptStatus.Failed };
        }
        VerifyExpirationSuspensionDates(login);
        //check device
        var status = CheckCanLoginImpl(login);
        // if we are ready to login, check external function to allow login
        if(status == LoginAttemptStatus.Success && _settings.CheckCanLoginFunc != null)
          status = _settings.CheckCanLoginFunc(context, login, status);
        switch(status) {
          case LoginAttemptStatus.Success:
            PostLoginActions actions = GetPostLoginActions(login);
            context.User = login.CreateUserInfo();
            //save prev value
            var prevLoggedInOn = login.LastLoggedInOn;
            OnLoginSucceeded(login);
            OnLoginEvent(context, LoginEventType.Login, login, userName: userName);
            return new LoginResult() {
              Status = status, Login = login, Actions = actions, User = context.User,
              SessionId = Guid.NewGuid(), LastLoggedInOn = prevLoggedInOn,
            };
          case LoginAttemptStatus.PendingMultifactor:
            OnLoginEvent(context, LoginEventType.LoginPendingMultiFactor, login, userName: userName);
            return new LoginResult() { Status = status, Login = login };
          case LoginAttemptStatus.AccountInactive:
            OnLoginEvent(context, LoginEventType.LoginFailed, login, "Login failed due to inactive status", userName: userName);
            return new LoginResult() { Status = status, Login = login };
          case LoginAttemptStatus.Failed:
          default:
            OnLoginFailed(login);
            OnLoginEvent(context, LoginEventType.LoginFailed, login, userName: userName);
            return new LoginResult() { Status = status };
        }//switch
      } finally {
        session.SaveChanges(); 
      }
    }//method

    // Used by OAuth
    public LoginResult LoginUser(OperationContext context, Guid userId) {
      var session = context.OpenSession();
      var login = session.EntitySet<ILogin>().Where(lg => lg.UserId == userId).FirstOrDefault();
      if(login == null || login.Flags.IsSet(LoginFlags.Inactive))
        return new LoginResult() { Status = LoginAttemptStatus.Failed };
      context.User = login.CreateUserInfo();
      OnLoginEvent(context, LoginEventType.Login, login);
      var lastLogin = login.LastLoggedInOn; //save prev value
      login.LastLoggedInOn = App.TimeService.UtcNow;
      OnLoginEvent(context, LoginEventType.Login, login, userName: login.UserName);
      session.SaveChanges();
      context.SessionId = Guid.NewGuid();
      return new LoginResult() { Status = LoginAttemptStatus.Success, Login = login, User = context.User, SessionId = context.SessionId.Value, LastLoggedInOn = lastLogin };
    }


    public LoginResult CompleteMultiFactorLogin(OperationContext context, ILogin login) {
      PostLoginActions actions = GetPostLoginActions(login);
      context.User = login.CreateUserInfo();
      var lastLogin = login.LastLoggedInOn;
      login.LastLoggedInOn = App.TimeService.UtcNow;
      var session = EntityHelper.GetSession(login);
      if(context.SessionId == null)
        context.SessionId = Guid.NewGuid(); 
      OnLoginEvent(context, LoginEventType.MultiFactorLoginCompleted, login);
      OnLoginEvent(context, LoginEventType.Login, login);
      OnLoginSucceeded(login);
      session.SaveChanges();
      return new LoginResult() {
        Status = LoginAttemptStatus.Success, Login = login, Actions = actions, User = context.User, SessionId = context.SessionId.Value,  LastLoggedInOn = lastLogin };
    }

    public void Logout(OperationContext context) {
      var user = context.User;
      Util.Check(user.Kind == UserKind.AuthenticatedUser, "Cannot logout - user is not authenticated.");
      var session = context.OpenSession();
      var login = session.EntitySet<ILogin>().Where(lg => lg.UserId == user.UserId).FirstOrDefault();
      if(login == null)
        return;
      OnLoginEvent(context, LoginEventType.Logout, login);
      context.Values.Clear();
    }

    //Low-level methods
    public ILogin FindLogin(IEntitySession session, string userName, string password, Guid? tenantId) {
      var context = session.Context;
      context.ValidateNotEmpty(userName, ClientFaultCodes.ValueMissing, "UserName", null, "UserName may not be empty");
      context.ValidateNotEmpty(password, ClientFaultCodes.ValueMissing, "Password", null, "Password may not be empty");
      context.ThrowValidation();
      userName = CheckUserName(context, userName);
      var userNameHash = _hashService.ComputeHash(userName);
      var tenantIdValue = tenantId == null ? Guid.Empty : tenantId.Value;
      var qryLogins = from lg in session.EntitySet<ILogin>()
                      where lg.UserNameHash == userNameHash && lg.UserName == userName
                      select lg;
      // Match password
      using(session.WithElevatedRead()) {
        var login = qryLogins.FirstOrDefault();
        return login;
      }
    }

    public LoginAttemptStatus CheckCanLogin(ILogin login, string deviceToken = null) {
      var device = login.GetDevice(deviceToken);
      return CheckCanLoginImpl(login, device); 
    }

    public PostLoginActions GetPostLoginActions(ILogin login) {
      // Success, figure out action flags --------------------------
      var actions = PostLoginActions.None;
      //Check if HashWorkFactor has changed since password was set - if yes, we need to reset password. 
      if(login.Flags.IsSet(LoginFlags.OneTimePassword) || login.HashWorkFactor != _settings.PasswordHasher.WorkFactor)
        actions |= PostLoginActions.ForceChangePassword;
      if(login.IncompleteFactors != ExtraFactorTypes.None)
        actions |= PostLoginActions.SetupExtraFactors;
      if(_settings.WarnPasswordExpiresDays > 0) {
        var expiresDays = login.GetExpiresDays();
        if(expiresDays < _settings.WarnPasswordExpiresDays)
          actions |= PostLoginActions.WarnPasswordExpires;
      }
      return actions; 
    }


    public bool CheckSecretQuestionAnswer(ISecretQuestionAnswer storedAnswer, string answer) {
      Util.Check(storedAnswer != null, "Stored answer may not be null");
      var hash = GetWeakSecretAnswerHash(answer, storedAnswer.Login.Id);
      if(hash == storedAnswer.AnswerHash)
        return true;
      var session = EntityHelper.GetSession(storedAnswer); 
      OnLoginEvent(session.Context, LoginEventType.QuestionAnswersFailed, storedAnswer.Login, "Question #" + storedAnswer.Number);
      return false; 
    }

    #endregion

    private LoginAttemptStatus CheckCanLoginImpl(ILogin login, ITrustedDevice device = null) {
      if(login.Flags.IsSet(LoginFlags.Inactive))
        return LoginAttemptStatus.AccountInactive;
      if(login.Flags.IsSet(LoginFlags.Suspended) && login.SuspendedUntil != null && login.SuspendedUntil > App.TimeService.UtcNow) 
        return login.Flags.IsSet(LoginFlags.DoNotConcealMembership) ?
            LoginAttemptStatus.AccountSuspended : LoginAttemptStatus.Failed;
      if(login.Flags.IsSet(LoginFlags.OneTimePassword)) {
        //If it was already used, login fails; otherwise succeed but mark it as used
        if(login.Flags.IsSet(LoginFlags.OneTimePasswordUsed))
          return LoginAttemptStatus.Failed;
        return LoginAttemptStatus.Success;
      }
      // multi-factor
      if(CheckNeedMultifactor(login, device))
        return LoginAttemptStatus.PendingMultifactor;
      return LoginAttemptStatus.Success;
    }

    public void VerifyExpirationSuspensionDates(ILogin login) {
      var session = EntityHelper.GetSession(login); 
      var utcNow = App.TimeService.UtcNow; 
      bool changed = false;
      if(login.Flags.IsSet(LoginFlags.Disabled))
        return; 
      if(login.Expires != null && login.Expires < utcNow) {
        login.Flags |= LoginFlags.PasswordExpired;
        login.Expires = null; 
        OnLoginEvent(session.Context, LoginEventType.PasswordExpired, login, "Password expired for " + login.UserName + " after expiration period ended.", userName: login.UserName);
        changed = true;
      }
      if(login.Flags.IsSet(LoginFlags.Suspended) && login.SuspendedUntil != null && login.SuspendedUntil.Value < utcNow) {
        login.Flags &= ~LoginFlags.Suspended;
        login.SuspendedUntil = null;
        OnLoginEvent(session.Context, LoginEventType.LoginReactivated, login, "Login " + login.UserName + " reactivated after suspend period ended.", userName: login.UserName);
        changed = true;
      }
      if(changed) {
        var updateQuery = session.EntitySet<ILogin>().Where(lg => lg.Id == login.Id)
          .Select(lg => new { Id = login.Id, Flags = login.Flags, SuspendedUntil = login.SuspendedUntil, Expires = login.Expires });
        session.ExecuteUpdate<ILogin>(updateQuery); 
      }
    }

    private void OnLoginFailed(ILogin login) {
      // If last failed long ago, reset count; otherwise increment
      var utcNow = App.TimeService.UtcNow; 
      var lastFailed = login.LastFailedLoginOn;
      if(lastFailed != null && lastFailed.Value.AddMinutes(_settings.SuspendOnFailMinutes) < utcNow)
        login.FailCount = 1;
      else
        login.FailCount++;
      login.LastFailedLoginOn = utcNow; 
      // Check if we need to suspend
      if(login.FailCount >= _settings.SuspendOnFailCount) {
        login.Flags |= LoginFlags.Suspended;
        login.SuspendedUntil = utcNow.AddMinutes(_settings.SuspendOnFailMinutes);
      }
    }

    private void OnLoginSucceeded(ILogin login) {
      if(login.Flags.IsSet(LoginFlags.OneTimePassword)) 
        login.Flags |= LoginFlags.OneTimePasswordUsed;
      if(login.FailCount > 0)
        login.FailCount = 0;
    }

    private bool CheckNeedMultifactor(ILogin login, ITrustedDevice device) {
      //Verify multi-factor 
      if(!login.Flags.IsSet(LoginFlags.RequireMultiFactor))
        return false;
      //check if device is trusted
      if(device != null && device.TrustLevel == DeviceTrustLevel.AllowSingleFactor)
        return false;
      return true;
    }

  }//module

}//ns
