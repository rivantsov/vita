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
using Vita.Modules.Logging;
using Vita.Modules.EncryptedData;
using Vita.Modules.ApiClients.Recaptcha;
using Vita.Modules.Notifications;

// See here for detailed discussion of password reset feature: 
// http://www.troyhunt.com/2012/05/everything-you-ever-wanted-to-know.html

namespace Vita.Modules.Login {

  public partial class LoginModule : EntityModule, ILoginService, ILoginManagementService, ILoginProcessService, ILoginAdministrationService {
    public static readonly Version CurrentVersion = new Version("1.1.0.0");
    public const string LoginIncidentType = "Login";

    LoginModuleSettings _settings;
    IIncidentLogService _incidentLog;
    ILoginLogService _loginLog;
    IUserSessionService _sessionService;
    IRecaptchaService _recaptchaService;
    INotificationService _notificationService;
    public readonly LoginAuthorizationRoles Roles;


    #region constructors and init
    public LoginModule(EntityArea area, LoginModuleSettings settings, string name = null) : base(area, name ?? "LoginModule", "Login module", version: CurrentVersion) {
      _settings = settings;
      App.RegisterConfig(_settings);
      Requires<EncryptedDataModule>();
      Requires<TextTemplates.TemplateModule>();
      //Register entities
      RegisterEntities(typeof(ILogin), typeof(ISecretQuestion), typeof(ISecretQuestionAnswer),  typeof(ITrustedDevice),
          typeof(ILoginExtraFactor), typeof(IPasswordHistory), typeof(ILoginProcess));
      //Register services
      App.RegisterService<ILoginService>(this);
      App.RegisterService<ILoginProcessService>(this);
      App.RegisterService<ILoginManagementService>(this);
      App.RegisterService<ILoginAdministrationService>(this);
      RegisterSize("EventType", 50);
      Roles = new LoginAuthorizationRoles(); 
      // Create recaptcha service if settings are there
      if (_settings.Recaptcha != null) {
        var recaptcha = new RecaptchaService(_settings.Recaptcha);
        App.RegisterService<IRecaptchaService>(recaptcha); 
      }
    }

    public override void Init() {
      base.Init();
      _recaptchaService = App.GetService<IRecaptchaService>(); 
      _loginLog = App.GetService<ILoginLogService>();
      _sessionService = App.GetService<IUserSessionService>();
      _incidentLog = App.GetService<IIncidentLogService>();
      _notificationService = App.GetService<INotificationService>();
      // automatically create notification service if it is not found - to ease upgrading after refactoring that introduced this service (Feb 2016)
      if (_notificationService == null) 
        _notificationService = NotificationService.Create(App);
      // Password checker
      if(_settings.PasswordChecker == null)
        _settings.PasswordChecker = new PasswordStrengthChecker(App);
      IEncryptionService encrService = App.GetService<IEncryptionService>();
      Util.Check(encrService != null, "Failed to get encryption service."); //never happens, module requires EncryptedDataModule
      if (!string.IsNullOrWhiteSpace(_settings.EncryptionChannelName))
        Util.Check(encrService.IsRegistered(_settings.EncryptionChannelName), 
          "Encryption channel '{0}' for LoginModule is not registered in EncryptedDataModule.");
      //Login failed trigger 
      if(_incidentLog != null && _settings.LoginFailedTriggerSettings != null) {
        var trigger = new LoginFailedTrigger(App, _settings.LoginFailedTriggerSettings);
        _incidentLog.AddTrigger(trigger);
      }

    }
    #endregion

    private string CheckUserName(OperationContext context, string userName) {
      context.ThrowIfEmpty(userName, ClientFaultCodes.ValueMissing, "UserName", userName, "User name may not be empty");
      userName = userName.Trim().ToLowerInvariant();
      context.ThrowIf(userName.Length < 3, LoginFaultCodes.UserNameTooShort, "UserName", userName, "User name is too short");
      return userName; 
    }

    private void LogIncident(OperationContext context, string incidentType, string subType, string message, ILogin login, string key) {
      if(_incidentLog == null)
        return;
      Guid? loginId = login == null ? (Guid?) null : login.Id; 
      _incidentLog.LogIncident(incidentType, message, subType, key1: key, keyId1: loginId, operationContext: context);
    }

    protected internal void OnLoginEvent(OperationContext context, LoginEventType eventType, ILogin login = null, string notes = null, string userName = null) {
      if(context == null && login != null)
        context = EntityHelper.GetSession(login).Context; 
      if(_loginLog != null)
        _loginLog.LogEvent(context, eventType, login, notes, userName);
      var args = new LoginEventArgs(eventType, login, context);
      if(LoginEvent != null)
        LoginEvent(this, args);
    }

    private bool VerifyPassword(ILogin login, string password) {
      var saltBytes = login.Id.ToByteArray();
      var ok = _settings.PasswordHasher.VerifyPassword(password, saltBytes, login.HashWorkFactor, login.PasswordHash);
      return ok;
    }
    private string HashPassword(string password, Guid loginId) {
      var saltBytes = loginId.ToByteArray();
      return _settings.PasswordHasher.HashPassword(password, saltBytes);
    }

    private void ChangeUserPassword(ILogin login, string password, bool oneTimeByAdmin) {
      Util.Check(login != null, "Login not found.");
      Util.Check(!string.IsNullOrWhiteSpace(password), "Password may not be empty.");
      var session = EntityHelper.GetSession(login);
      var userName = CheckUserName(session.Context, login.UserName);
      if (oneTimeByAdmin) // do not require strong, just check length > 5
        session.Context.ThrowIf(password.Length < 6, ClientFaultCodes.InvalidValue, "Password", "Password is too short.");
      else 
        CheckPasswordStrength(session.Context, password);
      login.UserName = userName;
      login.PasswordHash = HashPassword(password, login.Id);
      login.WeakPasswordHash = GetWeakPasswordHash(password);
      login.HashWorkFactor = _settings.PasswordHasher.WorkFactor;
      login.PasswordResetOn = App.TimeService.UtcNow;
      login.Flags &= ~LoginFlags.Inactive; //activate
      if(oneTimeByAdmin) {
        login.Flags &= ~LoginFlags.OneTimePasswordUsed;
        login.Flags |= LoginFlags.OneTimePassword;
        login.Expires = App.TimeService.UtcNow.Add(_settings.OneTimePasswordExpiration);
        var msg = StringHelper.SafeFormat("One-time password {0} is set for user {1}; set by user {2} ", 
          password, login.UserName, session.Context.User.UserName);
        OnLoginEvent(session.Context, LoginEventType.OneTimePasswordSet, login, msg);
      } else {
        login.Flags &= ~(LoginFlags.OneTimePassword | LoginFlags.OneTimePasswordUsed);
        SetPasswordExpiration(login);
        OnLoginEvent(session.Context, LoginEventType.PasswordChanged, login);
        LogIncident(session.Context, LoginIncidentType, LoginEventType.PasswordChanged.ToString(), "Password changed for user " + userName, login, userName);
        login.CreatePasswordHistoryEntry();
      }
      session.SaveChanges(); 
    }

    private void SetPasswordExpiration(ILogin login) {
      if(_settings.PasswordExpirationPeriod == null)
        login.Expires = null;
      else 
        login.Expires = App.TimeService.UtcNow.Add(_settings.PasswordExpirationPeriod.Value);
    }

    //Weak password hash is used for 2 things:
    // 1. For quick lookup of possibly matching login records without computing strong hash, to rule out bogus login attempts early
    // 2. For storing history of password hashes, to prevent reusing passwords after resets
    // 10 bits seems to be OK in the middle (not too short and not too long)
    private static int GetWeakPasswordHash(string value) {
      var result = Math.Abs(Util.StableHash(value)) % 1024; //leave only 10 bits
      return result; 
    }

    //Weak hashes used for indexing and obscuring secret question answers
    // Secret question answers are often within a limited set; we store short weak hash, not answer itself, and we store only 10 bits.
    private static int GetWeakSecretAnswerHash(string value, Guid loginId) {
      value = value.Trim().ToLowerInvariant();
      var result = Math.Abs(Util.StableHash(value + loginId.ToString())) % 1024;
      return result;
    }

 
  }//module

}//ns
