using System;
using System.Collections.Generic;
using System.Text;
using Vita.Entities;

namespace Vita.Modules.Login {
  public partial class LoginModule {

    private bool VerifyPassword(ILogin login, string password) {
      var weakPwdHash = GetWeakPasswordHash(password);
      // Check weak pwd hash
      if(login.WeakPasswordHash != weakPwdHash)
        return false; 
      // full pwd hash check
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
      if(oneTimeByAdmin) // do not require strong, just check length > 5
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
        var msg = Util.SafeFormat("One-time password {0} is set for user {1}; set by user {2} ",
          password, login.UserName, session.Context.User.UserName);
        OnLoginEvent(session.Context, LoginEventType.OneTimePasswordSet, login, msg);
      } else {
        login.Flags &= ~(LoginFlags.OneTimePassword | LoginFlags.OneTimePasswordUsed);
        SetPasswordExpiration(login);
        OnLoginEvent(session.Context, LoginEventType.PasswordChanged, login);
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
    private int GetWeakPasswordHash(string value) {
      var result = Math.Abs(_hashService.ComputeHash(value)) % 1024; //leave only 10 bits
      return result;
    }

    //Weak hashes used for indexing and obscuring secret question answers
    // Secret question answers are often within a limited set; we store short weak hash, not answer itself, and we store only 10 bits.
    private int GetWeakSecretAnswerHash(string value, Guid loginId) {
      value = value.Trim().ToLowerInvariant();
      var result = Math.Abs(_hashService.ComputeHash(value + loginId.ToString())) % 1024;
      return result;
    }


  }
}
