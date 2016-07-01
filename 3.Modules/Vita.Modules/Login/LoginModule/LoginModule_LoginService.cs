using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Web;
using Vita.Modules.Logging;
using Vita.Modules.EncryptedData;


namespace Vita.Modules.Login {

  public partial class LoginModule {

    #region ILoginService Members
    public event EventHandler<LoginEventArgs> LoginEvent;

    public LoginResult Login(OperationContext context, string userName, string password, Guid? tenantId = null,
                             string deviceToken = null) {
      context.ThrowIf(password.Length > 100, ClientFaultCodes.InvalidValue, "password", "Password too long, max size: 100.");
      var webCtx = context.WebContext;
      userName = CheckUserName(context, userName);
      var session = context.OpenSystemSession();
      var login = FindLogin(session, userName, password, tenantId);
      if(login == null) {
        if(webCtx != null)
          webCtx.Flags |= WebCallFlags.AttackRedFlag;
        OnLoginEvent(context, LoginEventType.LoginFailed, null, userName: userName);
        LogIncident(context, LoginIncidentType, LoginEventType.LoginFailed.ToString(), "User: " + userName, null, userName);
        return new LoginResult() { Status = LoginAttemptStatus.Failed };
      }
      var device = login.GetDevice(deviceToken);
      try {
        var status = CheckCanLoginImpl(login, device);
        //Check non-success statuses
        switch(status) {
          case LoginAttemptStatus.Success:
            PostLoginActions actions = GetPostLoginActions(login);
            context.User = login.CreateUserInfo();
            if(_sessionService != null)
              AttachUserSession(context, login, device);
            App.UserLoggedIn(context);
            var lastLogin = login.LastLoggedInOn; //save prev value
            UpdateLastLoggedInOn(login);
            OnLoginEvent(context, LoginEventType.Login, login, userName: userName);
            var sessionToken = context.UserSession == null ? null : context.UserSession.Token;
            return new LoginResult() { Status = status, Login = login, Actions = actions, User = context.User, SessionToken = sessionToken, LastLoggedInOn = lastLogin };
          case LoginAttemptStatus.PendingMultifactor:
            OnLoginEvent(context, LoginEventType.LoginPendingMultiFactor, login, userName: userName);
            return new LoginResult() { Status = status, Login = login };
          case LoginAttemptStatus.AccountInactive:
            OnLoginEvent(context, LoginEventType.LoginFailed, login, "Login failed due to inactive status", userName: userName);
            return new LoginResult() { Status = status, Login = login };
          case LoginAttemptStatus.Failed:
          default:
            OnLoginEvent(context, LoginEventType.LoginFailed, login, userName: userName);
            return new LoginResult() { Status = status };
        }
      } finally {
        session.SaveChanges();
      }
    }//method

    public LoginResult LoginUser(OperationContext context, Guid userId) {
      var session = context.OpenSystemSession();
      var login = session.EntitySet<ILogin>().Where(lg => lg.UserId == userId).FirstOrDefault();
      if(login == null || login.Flags.IsSet(LoginFlags.Inactive))
        return new LoginResult() { Status = LoginAttemptStatus.Failed };
      context.User = login.CreateUserInfo();
      if(_sessionService != null)
        AttachUserSession(context, login);
      App.UserLoggedIn(context);
      var lastLogin = login.LastLoggedInOn; //save prev value
      UpdateLastLoggedInOn(login);
      OnLoginEvent(context, LoginEventType.Login, login, userName: login.UserName);
      var sessionToken = context.UserSession == null ? null : context.UserSession.Token;
      return new LoginResult() { Status = LoginAttemptStatus.Success, Login = login, User = context.User, SessionToken = sessionToken, LastLoggedInOn = lastLogin };
    }


    public LoginResult CompleteMultiFactorLogin(OperationContext context, ILogin login) {
      PostLoginActions actions = GetPostLoginActions(login);
      context.User = login.CreateUserInfo();
      var lastLogin = login.LastLoggedInOn;
      UpdateLastLoggedInOn(login);
      AttachUserSession(context, login);
      OnLoginEvent(context, LoginEventType.MultiFactorLoginCompleted, login);
      App.UserLoggedIn(context);
      return new LoginResult() {
        Status = LoginAttemptStatus.Success, Login = login, Actions = actions, User = context.User, SessionToken = context.UserSession.Token, LastLoggedInOn = lastLogin };
    }

    public void Logout(OperationContext context) {
      var user = context.User;
      Util.Check(user.Kind == UserKind.AuthenticatedUser, "Cannot logout - user is not authenticated.");
      var session = context.OpenSystemSession();
      var login = session.EntitySet<ILogin>().Where(lg => lg.UserId == user.UserId).FirstOrDefault();
      if(login == null)
        return;
      OnLoginEvent(context, LoginEventType.Logout, login);
      App.UserLoggedOut(context);
      if(_sessionService != null)
        _sessionService.EndSession(context);
      context.Values.Clear();
    }

    //Low-level methods
    public ILogin FindLogin(IEntitySession session, string userName, string password, Guid? tenantId) {
      var context = session.Context;
      context.ValidateNotEmpty(userName, ClientFaultCodes.ValueMissing, "UserName", null, "UserName may not be empty");
      context.ValidateNotEmpty(password, ClientFaultCodes.ValueMissing, "Password", null, "Password may not be empty");
      context.ThrowValidation();
      userName = CheckUserName(context, userName);
      var userNameHash = Util.StableHash(userName);
      var weakPwdHash = GetWeakPasswordHash(password);
      var tenantIdValue = tenantId == null ? Guid.Empty : tenantId.Value;
      // Note: we do not compare usernames, only UserNameHash values; UserName might be null if we don't save them
      var qryLogins = from lg in session.EntitySet<ILogin>()
                      where lg.UserNameHash == userNameHash && lg.WeakPasswordHash == weakPwdHash
                        && lg.TenantId == tenantIdValue
                      select lg;
      //Query logins table
      using(session.WithElevateRead()) {
        var logins = qryLogins.ToList(); //these are candidates, but most often will be just one
        var login = logins.FirstOrDefault(lg => VerifyPassword(lg, password));
        if(login != null)
          VerifyExpirationSuspensionDates(login);
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
      if(login.Flags.IsSet(LoginFlags.OneTimePassword)) {
        //If it was already used, login fails; otherwise succeed but mark it as used
        if(login.Flags.IsSet(LoginFlags.OneTimePasswordUsed))
          return LoginAttemptStatus.Failed;
        login.Flags |= LoginFlags.OneTimePasswordUsed;
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
      if(login.Flags.IsSet(LoginFlags.Suspended) && login.SuspendedUntil != null && utcNow > login.SuspendedUntil) {
        login.Flags &= ~LoginFlags.Suspended;
        login.SuspendedUntil = null;
        OnLoginEvent(session.Context, LoginEventType.LoginReactivated, login, "Login " + login.UserName + " reactivated after suspend period ended.", userName: login.UserName);
        changed = true;
      }
      if(changed)
        session.SaveChanges();
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

    private void AttachUserSession(OperationContext context, ILogin login, ITrustedDevice device = null) {
      if(_sessionService == null)
        return; 
      //Start session for logged in user and get session token
      var trustLevel = DeviceTrustLevel.None;
      if(device != null) {
        trustLevel = device.TrustLevel;
        device.LastLoggedIn = App.TimeService.UtcNow; 
      }
      if(context.UserSession != null) {
        context.UserSession.User = context.User;
        _sessionService.UpdateSession(context);
        return; 
      } 
      //New session
      UserSessionExpiration expir = null; 
      if (trustLevel == DeviceTrustLevel.KeepLoggedIn)
        expir = new UserSessionExpiration() { ExpirationType = UserSessionExpirationType.KeepLoggedIn };
      context.UserSession = _sessionService.StartSession(context, context.User, expir);
    }

  }//module

}//ns
