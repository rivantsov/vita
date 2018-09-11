using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services; 

namespace Vita.Modules.Login {

  public static class LoginExtensions {
    public static bool IsSet(this LoginModuleOptions options, LoginModuleOptions option) {
      return (options & option) != 0;
    }
    public static bool IsSet(this PostLoginActions actions, PostLoginActions action) {
      return (actions & action) != 0;
    }
    public static bool IsSet(this LoginFlags flags, LoginFlags flag) {
      return (flags & flag) != 0;
    }
    public static LoginFlags SetMasked(this LoginFlags flags, LoginFlags newFlags, LoginFlags mask) {
      return (flags & ~mask) | (newFlags & mask); 
    }
    public static bool IsSet(this CharOptions flags, CharOptions flag) {
      return (flags & flag) != 0;
    }
    public static LoginFlags SetValue(this LoginFlags flags, LoginFlags flag, bool value) {
      if(value)
        return flags | flag;
      else
        return flags & ~flag; 
    }

    public static bool IsSet(this SecretQuestionFlags flags, SecretQuestionFlags flag) {
      return (flags & flag) != 0;
    }
    public static bool IsSet(this LoginProcessFlags flags, LoginProcessFlags flag) {
      return (flags & flag) != 0;
    }

    internal static UserInfo CreateUserInfo(this ILogin login) {
      return new UserInfo(login.UserId, login.UserName, UserKind.AuthenticatedUser, login.AltUserId);
    }

    public static bool IsSet(this ExtraFactorTypes factors, ExtraFactorTypes factor) {
      return (factors & factor) != 0;
    }

    public static int GetExpiresDays(this ILogin login) {
      if(login.Expires == null)
        return 10000;
      var expValue = login.Expires.Value;
      var rec = EntityHelper.GetRecord(login);
      var now = rec.Session.Context.App.TimeService.UtcNow; 
      var expiresDays = (int)Math.Floor(expValue.Subtract(now).TotalDays);
      return expiresDays; 
    }

    public static LoginEventType ToEventType(this LoginAttemptStatus status) {
      switch(status) {
        case LoginAttemptStatus.Success: return LoginEventType.Login;
        case LoginAttemptStatus.PendingMultifactor: return LoginEventType.LoginPendingMultiFactor;
        default:
          return LoginEventType.LoginFailed;
      }
    }

    public static ISecretQuestionAnswer AddSecretQuestionAnswer(this ILogin login, int number, ISecretQuestion question, int answerHash) {
      Util.Check(login != null, "Login may not be null");
      Util.Check(question != null, "SecretQuestion may not be null");
      var session = EntityHelper.GetSession(login);
      var qa = session.NewEntity<ISecretQuestionAnswer>();
      qa.Login = login;
      qa.Number = number;
      qa.Question = question;
      qa.AnswerHash = answerHash;
      return qa;
    }

    public static ISecretQuestionAnswer AddPrivateSecretQuestionAndAnswer(this ILogin login, int number, string question, int answerHash) {
      Util.Check(login != null, "Login may not be null");
      var session = EntityHelper.GetSession(login);
      var entQ = session.NewEntity<ISecretQuestion>();
      entQ.Question = question;
      entQ.Number = 1000;
      entQ.Flags = SecretQuestionFlags.Private;
      var qa = AddSecretQuestionAnswer(login, number, entQ, answerHash);
      return qa;
    }

    public static ITrustedDevice GetDevice(this ILogin login, string token) {
      if(string.IsNullOrWhiteSpace(token))
        return null;
      var session = EntityHelper.GetSession(login); 
      var device = session.EntitySet<ITrustedDevice>().Where(d => d.Login == login && d.Token == token).FirstOrDefault();
      return device;
    }

    public static ITrustedDevice NewTrustedDevice(this ILogin login, string token, DeviceType type, DeviceTrustLevel trustLevel) {
      var session = EntityHelper.GetSession(login);
      var ent = session.NewEntity<ITrustedDevice>();
      ent.Login = login;
      ent.Token = token;
      ent.TrustLevel = trustLevel;
      ent.Type = type; 
      ent.LastLoggedIn = session.Context.App.TimeService.UtcNow;
      return ent; 
    }

    public static bool HasExtraFactor(this ILogin login, ExtraFactorTypes factorType) {
      return login.ExtraFactors.Any(f => f.FactorType == factorType);
    }

    public static void SetVerified(this ILoginExtraFactor factor, DateTime now) {
      Util.Check(factor != null, "factor parameter may not be null.");
      var session = EntityHelper.GetSession(factor);
      factor.VerifiedOn = now; 
    }

  }//class
}//ns
