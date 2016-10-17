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

namespace Vita.Modules.Login {
  using Api; 

  public partial class LoginModule{

    #region ILoginManagementService members
    public ILogin NewLogin(IEntitySession session, string userName, string password,
            DateTime? expires = null, LoginFlags flags = LoginFlags.None, Guid? loginId = null, 
           Guid? userId = null, Int64? altUserId = null, Guid? tenantId = null) {
      userName = CheckUserName(session.Context, userName);
      CheckPasswordStrength(session.Context, password);
      var login = session.NewEntity<ILogin>();
      if(loginId != null)
        login.Id = loginId.Value;
      login.UserName = userName;
      login.UserNameHash = Util.StableHash(userName);
      login.PasswordHash = HashPassword(password, login.Id);
      login.TenantId = (tenantId == null) ? Guid.Empty : tenantId.Value;
      login.Flags = flags;
      login.HashWorkFactor = _settings.PasswordHasher.WorkFactor;
      login.WeakPasswordHash = GetWeakPasswordHash(password);
      login.PasswordResetFactors = _settings.DefaultPasswordResetFactors;
      login.MultiFactorLoginFactors = ExtraFactorTypes.Email; //default, but multi-factor is not enabled yet
      login.IncompleteFactors = login.PasswordResetFactors;
      if (userId != null)
        login.UserId = userId.Value;
      if(altUserId != null)
        login.AltUserId = altUserId.Value;
      var utcNow = App.TimeService.UtcNow;
      if(expires != null)
        login.Expires = expires;
      else
        SetPasswordExpiration(login);
      login.PasswordResetOn = utcNow;
      login.CreatePasswordHistoryEntry();
      OnLoginEvent(session.Context, LoginEventType.LoginCreated, login);
      return login;
    }

    public ILogin GetLogin(IEntitySession session) {
      var context = session.Context;
      var user = context.User;
      context.ThrowIf(user.Kind != UserKind.AuthenticatedUser, ClientFaultCodes.InvalidValue, "User", "User must be authenticated.");
      var query = session.EntitySet<ILogin>();
      if(user.UserId == Guid.Empty)
        query = query.Where(lg => lg.AltUserId == user.AltUserId);
      else
        query = query.Where(lg => lg.UserId == user.UserId);
      var login = query.FirstOrDefault();
      return login; 
    }

    public void UpdateLogin(ILogin login, LoginInfo loginInfo) {
      var session = EntityHelper.GetSession(login);
      login.MultiFactorLoginFactors = loginInfo.MultiFactorLoginFactors;
      var emailOrPhone = loginInfo.PasswordResetFactors.IsSet(ExtraFactorTypes.Email | ExtraFactorTypes.Phone);
      var questions = loginInfo.PasswordResetFactors.IsSet(ExtraFactorTypes.SecretQuestions);
      session.Context.ThrowIf(!(emailOrPhone & questions), ClientFaultCodes.InvalidValue, "PasswordResetFactors",
        "Invalid password reset factors: must use email or phone, and secret questions.");
      login.PasswordResetFactors = loginInfo.PasswordResetFactors;
      var flagMask = LoginFlags.RequireMultiFactor | LoginFlags.DoNotConcealMembership;
      login.Flags = loginInfo.Flags.SetMasked(loginInfo.Flags, flagMask);
      if(loginInfo.Flags.IsSet(LoginFlags.RequireMultiFactor)) {
        session.Context.ThrowIf(loginInfo.MultiFactorLoginFactors == ExtraFactorTypes.None, ClientFaultCodes.InvalidValue, "MultiFactorLoginFactors",
           "Cannot enable multi-factor login - at least one extra factor must be specified.");
        login.MultiFactorLoginFactors = loginInfo.MultiFactorLoginFactors;
      }
      var notes = StringHelper.SafeFormat("User: {0}, Flags: {1}, login multi-factors: '{2}', PasswordResetFactors: '{3}'",
           loginInfo.UserName, loginInfo.Flags, loginInfo.MultiFactorLoginFactors, 
           loginInfo.PasswordResetFactors);
      OnLoginEvent(session.Context, LoginEventType.LoginChanged, login, notes: notes);
      session.SaveChanges(); 
    }

    public IList<ISecretQuestion> GetAllSecretQuestions(IEntitySession session) {
      var mask = SecretQuestionFlags.Disabled | SecretQuestionFlags.Private;
      var query = from q in session.EntitySet<ISecretQuestion>()
                  where (q.Flags & mask) == 0 //we look for public and enabled
                  orderby q.Number
                  select q;
      var questions = query.ToList();
      return questions;
    }

    public IList<ISecretQuestion> GetUserSecretQuestions(ILogin login) {
      var session = EntityHelper.GetSession(login);
      var query = from qa in session.EntitySet<ISecretQuestionAnswer>()
                  where qa.Login == login
                  orderby qa.Number
                  select qa.Question;
      var list = query.ToList();
      return list; 
    }

    public ISecretQuestionAnswer AddSecretQuestionAnswer(ILogin login, int number, ISecretQuestion question, string answer) {
      var hash = GetWeakSecretAnswerHash(answer, login.Id);
      return login.AddSecretQuestionAnswer(number, question, hash);
    } 

    public void UpdateUserQuestionAnswers(ILogin login, IList<SecretQuestionAnswer> answers) {
      var session = EntityHelper.GetSession(login); 
      // delete old ones
      foreach(var oldAns in login.SecretQuestionAnswers) {
        session.DeleteEntity(oldAns);
      }
      session.SaveChanges(); 
      //now add new 
      int number = 0;
      foreach(var ans in answers) {
        var ansHash = GetWeakSecretAnswerHash(ans.Answer, login.Id);
        var question = session.GetEntity<ISecretQuestion>(ans.QuestionId, LoadFlags.Stub);
        var iAns = login.AddSecretQuestionAnswer(number, question, ansHash);
        number++;
      }
      CheckLoginFactorsSetupCompleted(login);
      session.SaveChanges();
    }

    public void ReorderUserQuestionAnswers(ILogin login, IList<Guid> ids) {
      var session = EntityHelper.GetSession(login);
      session.Context.ThrowIf(ids.Count != login.SecretQuestionAnswers.Count, ClientFaultCodes.InvalidValue, "IDs", 
           "Number of IDs ({0}) does not match user question answers count ({1}).",  ids.Count, login.SecretQuestionAnswers.Count); 
      int number = 0;
      foreach(var id in ids) {
        var ans = login.SecretQuestionAnswers.FirstOrDefault(a => a.Question.Id == id);
        session.Context.ThrowIfNull(ans, ClientFaultCodes.InvalidValue, "AnswerId", "Question/answer not found for ID: {0}", id);
        ans.Number = number++;
      }
      session.SaveChanges(); 
    }

    public IList<LoginExtraFactor> GetUserFactors(ILogin login) {
      var result = new List<LoginExtraFactor>();
      foreach(var f in login.ExtraFactors) 
        result.Add(new LoginExtraFactor() {Id = f.Id, Type = f.FactorType, Confirmed = f.VerifiedOn != null, 
          Value = f.Info.DecryptString(_settings.EncryptionChannelName)});
      return result;      
    }

    public LoginExtraFactor GetUserFactor(ILogin login, Guid factorId) {
      var session = EntityHelper.GetSession(login);
      var factor = session.GetEntity<ILoginExtraFactor>(factorId);
      if (factor == null || factor.Login != login)
        return null;
      return ToModel(factor); 
    }

    public string FindLoginFactor(ILogin login, ExtraFactorTypes factorType) {
      var session = EntityHelper.GetSession(login);
      var factor = session.EntitySet<ILoginExtraFactor>().Where(f => f.Login == login && f.FactorType == factorType).FirstOrDefault();
      if (factor == null)
        return null;
      var strFactor = factor.Info.DecryptString(_settings.EncryptionChannelName);
      return strFactor;
    }


    public LoginExtraFactor AddFactor(ILogin login, ExtraFactorTypes type, string value) {
      var session = EntityHelper.GetSession(login);
      if (type == ExtraFactorTypes.GoogleAuthenticator)
        value = GoogleAuthenticator.GoogleAuthenticatorUtil.GenerateSecret();
      var factor = session.NewEntity<ILoginExtraFactor>();
      factor.Login = login;
      factor.FactorType = type;
      factor.Info = session.NewOrUpdate(factor.Info, value, _settings.EncryptionChannelName);
      factor.InfoHash = Util.StableHash(value);
      if (type == ExtraFactorTypes.GoogleAuthenticator)
        factor.SetVerified(App.TimeService.UtcNow);
      return ToModel(factor); 
    }

    public LoginExtraFactor UpdateFactor(ILoginExtraFactor fc, string value) {
      var session = EntityHelper.GetSession(fc);
      if (fc.FactorType == ExtraFactorTypes.GoogleAuthenticator)
        value = GoogleAuthenticator.GoogleAuthenticatorUtil.GenerateSecret();
      fc.Info = session.NewOrUpdate(fc.Info, value, _settings.EncryptionChannelName);
      fc.InfoHash = Util.StableHash(value);
      if (fc.FactorType == ExtraFactorTypes.GoogleAuthenticator)
        fc.SetVerified(App.TimeService.UtcNow);
      else 
        fc.VerifiedOn = null;
      return ToModel(fc);
    }

    public string GetGoogleAuthenticatorQRUrl(ILoginExtraFactor factor) {
      Util.Check(factor.FactorType == ExtraFactorTypes.GoogleAuthenticator, "The extra factor type should be GoogleAuthenticator.");
      var secret = factor.Info.DecryptString(_settings.EncryptionChannelName);
      var identity = StringHelper.SafeFormat(_settings.GoogleAuthenticatorIdentityTemplate, App.AppName, factor.Login.UserName);
      var url = GoogleAuthenticator.GoogleAuthenticatorUtil.GetQRUrl(identity, secret);
      return url; 
    }

    public bool CheckLoginFactorsSetupCompleted(ILogin login) {
      var session = EntityHelper.GetSession(login);
      var hadPendingChanges = session.GetChangeCount() > 0; //true if there are pending changes
      //Password reset
      if (login.PasswordResetFactors == ExtraFactorTypes.None)
        login.PasswordResetFactors = _settings.DefaultPasswordResetFactors;
      var incompleteFactors = GetIncompleteFactors(login);
      if(login.IncompleteFactors != incompleteFactors)
        login.IncompleteFactors = incompleteFactors; 
      if (!hadPendingChanges) //save changes unless there were already changes so caller will save all
        session.SaveChanges();
      return login.IncompleteFactors == ExtraFactorTypes.None;
    }

    private ExtraFactorTypes GetIncompleteFactors(ILogin login) {
      var allFactorTypes = new[] { ExtraFactorTypes.Email, ExtraFactorTypes.Phone, ExtraFactorTypes.SecretQuestions };
      var requiredFactors = login.PasswordResetFactors | login.MultiFactorLoginFactors;
      var incompleteFactors = ExtraFactorTypes.None;
      foreach(var fType in allFactorTypes) {
        if(login.PasswordResetFactors.IsSet(fType)) {
          if(!FactorSetupCompleted(login, fType))
            incompleteFactors |= fType;
        }
      }
      return incompleteFactors; 
    }

    private bool FactorSetupCompleted(ILogin login, ExtraFactorTypes type) {
      switch(type) {
        case ExtraFactorTypes.SecretQuestions:
          return login.SecretQuestionAnswers.Count >= _settings.MinQuestionsCount;
        case ExtraFactorTypes.Email: 
        case ExtraFactorTypes.Phone:
          var factor = login.ExtraFactors.FirstOrDefault(f => f.FactorType == type && f.VerifiedOn != null);
          return factor != null;
        default:
          return false; //never happens
      }
    }

    public void ChangePassword(ILogin login, string oldPassword, string password) {
      var checkOld = _settings.Options.IsSet(LoginModuleOptions.AskOldPasswordOnChange);
      var oldIsOneTime = login.Flags.IsSet(LoginFlags.OneTimePassword);
      if (checkOld && ! oldIsOneTime) {
        var pwdHash = GetWeakPasswordHash(oldPassword);
        var session = EntityHelper.GetSession(login);
        session.Context.ThrowIf(login.WeakPasswordHash != pwdHash, ClientFaultCodes.InvalidValue, "OldPassword", "Invalid old password.");
      }
      ChangeUserPassword(login, password, false);
    }


    public ITrustedDevice RegisterTrustedDevice(ILogin login, DeviceType type, DeviceTrustLevel trustLevel) {
      var session = EntityHelper.GetSession(login); 
      var deviceToken = RandomHelper.GenerateRandomString(10);
      var device = login.NewTrustedDevice(deviceToken, type, trustLevel);
      session.SaveChanges(); 
      return device; 
    }

    public bool RemoveTrustedDevice(ILogin login, string deviceToken) {
      var device = login.GetDevice(deviceToken);
      if(device == null)
        return false;
      var session = EntityHelper.GetSession(login);
      session.DeleteEntity(device);
      session.SaveChanges();
      return true;     
    }
    
    public bool CheckPasswordWasUsed(ILogin login, string password, TimeSpan? period, int? lastUsedCount) {
      var session = EntityHelper.GetSession(login);
      var hash = GetWeakPasswordHash(password);
      var where = session.NewPredicate<IPasswordHistory>()
        .And(h => h.Login == login);
      if(period != null) {
        var startDate = session.Context.App.TimeService.UtcNow.Subtract(period.Value);
        where = where.And(h => h.DisabledOn == null || h.DisabledOn > startDate);
      }
      IQueryable<IPasswordHistory> query = session.EntitySet<IPasswordHistory>().Where(where).OrderByDescending(h => h.CreatedOn); ;
      if(lastUsedCount != null)
        query = query.Take(lastUsedCount.Value);
      var list = query.ToList();
      var match = list.FirstOrDefault(h => h.WeakPasswordHash == hash);
      return match != null;
    }

    public PasswordStrength EvaluatePasswordStrength(string password) {
      return _settings.PasswordChecker.Evaluate(password);
    }
    #endregion

    private void CheckPasswordStrength(OperationContext context, string password) {
      context.ThrowIfEmpty(password, ClientFaultCodes.InvalidValue, "password", "Password may not be empty.");
      context.ThrowIf(password.Length > 100, ClientFaultCodes.InvalidValue, "password", "Password too long, max size: 100.");
      var pwdStrength = EvaluatePasswordStrength(password);
      var pwdOk = pwdStrength >= _settings.RequiredPasswordStrength;
      context.ThrowIf(!pwdOk, LoginFaultCodes.WeakPassword, "Password", "Password does not meet strength criteria");
    }


    private LoginExtraFactor ToModel(ILoginExtraFactor factor) {
      var objFactor = new LoginExtraFactor() {
        Id = factor.Id, Type = factor.FactorType, Confirmed = factor.VerifiedOn != null,
        Value = factor.Info.DecryptString(_settings.EncryptionChannelName)
      };
      return objFactor;
    }


  }// class

}//ns
