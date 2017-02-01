using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.TextTemplates;

namespace Vita.Modules.Login {
  using Api;
  using Vita.Modules.Email;
  using Vita.Modules.Notifications; 

  public partial class LoginModule {

    public ILogin GetLogin(IEntitySession session, Guid loginId) {
      return session.GetEntity<ILogin>(loginId); 
    }

    //Login admin - add interface for admins
    public SearchResults<ILogin> SearchLogins(OperationContext context, LoginSearch search) {
      var utcNow = context.App.TimeService.UtcNow;
      var session = context.OpenSession();
      search = search.DefaultIfNull(take: 20, defaultOrderBy: "UserName");
      var where = session.NewPredicate<ILogin>()
        .AndIfNotEmpty(search.TenantId, lg => lg.TenantId == search.TenantId.Value)
        .AndIfNotEmpty(search.UserName, lg => lg.UserName.StartsWith(search.UserName))
        .AndIfNotEmpty(search.UserId, lg => lg.UserId == search.UserId)
        .AndIfNotEmpty(search.ExpiringBefore, lg => lg.Expires != null && lg.Expires < search.ExpiringBefore.Value)
        .AndIfNotEmpty(search.CreatedAfter, lg => lg.CreatedOn >= search.CreatedAfter.Value)
        .AndIfNotEmpty(search.CreatedBefore, lg => lg.CreatedOn <= search.CreatedBefore.Value)
        .AndIf(search.EnabledOnly, lg => (lg.Flags & LoginFlags.Disabled) == 0)
        .AndIf(search.SuspendedOnly, lg => (lg.Flags & LoginFlags.Suspended) == 0 && lg.SuspendedUntil > utcNow);
      if (!string.IsNullOrWhiteSpace(search.Email)) {
        var factorHash = Util.StableHash(search.Email.Trim().ToLowerInvariant());
        var subQuery = session.EntitySet<ILoginExtraFactor>().Where(f => f.InfoHash == factorHash).Select(f => f.Login.Id);
        where = where.And(lg => subQuery.Contains(lg.Id));
      };
      var result = session.ExecuteSearch(where, search);
      if(LoginExtensions.CheckSuspensionEnded(result.Results, utcNow)) {
        

      }
      return result; 
    }

    public string GenerateTempPassword() {
      if(_settings.Options.IsSet(LoginModuleOptions.GenerateSimpleTempPasswords))
        return RandomHelper.GenerateSafeRandomWord(7);
      else
        return _settings.PasswordChecker.GenerateStrongPassword(); //will generate StrongPassword
    }

    public void SetOneTimePassword(ILogin login, string password) {
      var session = EntityHelper.GetSession(login);
      ChangeUserPassword(login, password, oneTimeByAdmin: true);
      session.SaveChanges(); 
      var email = FindLoginFactor(login, ExtraFactorTypes.Email);
      if(email != null)
        SendNotification(session.Context, LoginNotificationTypes.OneTimePassword, NotificationMediaTypes.Email, email, login.UserId,  
          new Dictionary<string, object>() { 
          { LoginNotificationKeys.UserName, login.UserName}, { LoginNotificationKeys.OneTimePassword, password } });
    }

    public void UpdateStatus(ILogin login, bool? disabled, bool? suspended){
      var session = EntityHelper.GetSession(login); 
      var oldFlags = login.Flags;
      UpdateLoginFlag(login, LoginFlags.Disabled, disabled);
      UpdateLoginFlag(login, LoginFlags.Suspended, suspended);
      if(oldFlags == login.Flags)
        return;
      if(!login.Flags.IsSet(LoginFlags.Suspended))
        login.SuspendedUntil = null; 
      session.SaveChanges(); 
      //Write log
      var notes = string.Format( "Login status changed; old flags: {0}, new flags: {1}.", oldFlags, login.Flags); 
      OnLoginEvent(session.Context, LoginEventType.LoginChanged, login, notes);
      var disFlags = LoginFlags.Disabled | LoginFlags.Suspended;
      if (oldFlags.IsSet(disFlags) && !login.Flags.IsSet(disFlags))
        OnLoginEvent(session.Context, LoginEventType.LoginReactivated, login, StringHelper.SafeFormat("Login {0} reactivated.", login.UserName));
      else if(!oldFlags.IsSet(LoginFlags.Disabled) && login.Flags.IsSet(LoginFlags.Disabled))
        OnLoginEvent(session.Context, LoginEventType.LoginDisabled, login, StringHelper.SafeFormat("Login {0} disabled.", login.UserName));
      else if(!oldFlags.IsSet(LoginFlags.Suspended) && login.Flags.IsSet(LoginFlags.Suspended))
        OnLoginEvent(session.Context, LoginEventType.LoginDisabled, login, StringHelper.SafeFormat("Login {0} suspended.", login.UserName));
    }

    private void UpdateLoginFlag(ILogin login, LoginFlags flag, bool? value) {
      if(value == null) return;
      if(value.Value)
        login.Flags |= flag;
      else
        login.Flags &= ~flag;
    }

  }
}
