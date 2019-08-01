using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Services;

// See here for detailed discussion of password reset feature: 
// http://www.troyhunt.com/2012/05/everything-you-ever-wanted-to-know.html

namespace Vita.Modules.Login {

  public partial class LoginModule : EntityModule, ILoginService, ILoginManagementService, ILoginProcessService, ILoginAdministrationService {
    public static readonly Version CurrentVersion = new Version("2.0.0.0");

    LoginModuleSettings _settings;
    ILogService _logService; 

    IHashingService _hashService;
    // public readonly LoginAuthorizationRoles Roles;

    #region constructors and init
    public LoginModule(EntityArea area, LoginModuleSettings settings, string name = null) : base(area, name ?? "LoginModule", "Login module", version: CurrentVersion) {
      _settings = settings;
      App.RegisterConfig(_settings);
      //Register entities
      RegisterEntities(typeof(ILogin), typeof(ISecretQuestion), typeof(ISecretQuestionAnswer),  
        typeof(ITrustedDevice), typeof(ILoginExtraFactor), typeof(ILoginProcess));
      //Register services
      App.RegisterService<ILoginService>(this);
      App.RegisterService<ILoginProcessService>(this);
      App.RegisterService<ILoginManagementService>(this);
      App.RegisterService<ILoginAdministrationService>(this);
      RegisterSize("EventType", 50);
    }

    public override void Init() {
      base.Init();
      _logService = App.GetService<ILogService>();
      _hashService = App.GetService<IHashingService>(); 
      // Password checker
      if(_settings.PasswordChecker == null)
        _settings.PasswordChecker = new PasswordStrengthChecker(App);
    }
    #endregion

    private string CheckUserName(OperationContext context, string userName) {
      context.ThrowIfEmpty(userName, ClientFaultCodes.ValueMissing, "UserName", userName, "User name may not be empty");
      userName = userName.Trim().ToLowerInvariant();
      context.ThrowIf(userName.Length < 3, LoginFaultCodes.UserNameTooShort, "UserName", userName, "User name is too short");
      return userName; 
    }

    protected internal void OnLoginEvent(OperationContext context, LoginEventType eventType, 
            ILogin login = null, string message = null, string userName = null) {
      if(context == null && login != null)
        context = EntityHelper.GetSession(login).Context; 
      if(_logService != null)
        _logService.AddEntry(new AppEventEntry("Login", eventType.ToString(), EventSeverity.Info, 
              message: message,  objectId: login?.Id, context: context, objectName: userName));
      var args = new LoginEventArgs(eventType, login, context);
      LoginEvent?.Invoke(this, args);
    }

  }//module

}//ns
