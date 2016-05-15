using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Modules.Logging;

namespace Vita.Modules.Login {

  public class LoginFailedTriggerSettings {
    public int FailureCount;   // # of incidents to trigger action
    public TimeSpan TimeWindow;
    public TimeSpan SuspensionPeriod;

    public LoginFailedTriggerSettings(int failureCount, TimeSpan timeWindow, TimeSpan suspensionPeriod) {
      FailureCount = failureCount;
      TimeWindow = timeWindow;
      SuspensionPeriod = suspensionPeriod;
    }
    public LoginFailedTriggerSettings() {
      FailureCount = 5;
      TimeWindow = TimeSpan.FromMinutes(1);
      SuspensionPeriod = TimeSpan.FromMinutes(10);
    }
  }

  /// <summary>
  /// A trigger to watch the incident log for multiple failed login attempts and suspend the login account 
  /// after certain number of failures in a given period of time.
  /// </summary>
  public class LoginFailedTrigger : IncidentTrigger {
    EntityApp _app;
    public readonly LoginFailedTriggerSettings Settings;

    public LoginFailedTrigger(EntityApp app, LoginFailedTriggerSettings settings = null)  : base(LoginModule.LoginIncidentType, LoginEventType.LoginFailed.ToString()) {
      _app = app;
      Settings = settings ?? new LoginFailedTriggerSettings(); 
    }

    public override void OnNewIncident(IIncidentLog newEntry) {
      //if login failed with user name that matches a login in database, we log this loginId in KeyId1
      var failedUserName = newEntry.Key1;
      if(string.IsNullOrWhiteSpace(failedUserName))
        return;
      var tenantId = newEntry.KeyId2;

      var utcNow = _app.TimeService.UtcNow;
      var session = _app.OpenSystemSession();
      var fromDate = utcNow.Subtract(Settings.TimeWindow);
      var strLoginFailed = LoginEventType.LoginFailed.ToString();
      var qryRecentFailures = from lg in session.EntitySet<IIncidentLog>()
                              where lg.CreatedOn >= fromDate && lg.Key1 == failedUserName 
                              && lg.Type == LoginModule.LoginIncidentType && lg.SubType == strLoginFailed 
                              select lg;
      //Note: currently LINQ translator does not handle correctly comparing nullable values, so adding this separately
      if(tenantId != null)
        qryRecentFailures = qryRecentFailures.Where(lg => lg.KeyId2 == tenantId.Value);
      var failCount = qryRecentFailures.Count();
      if(failCount < Settings.FailureCount)
        return; //not yet
      // We have repeated login failures in short period of time; this might be an attack - suspend account(s) for several minutes
      // find Login(s) - might be more than one - we may have the same username for different tenants
      var loginSet = session.EntitySet<ILogin>();
      var loginQuery = from lg in loginSet
                         where lg.UserName == failedUserName
                         select lg;
      if(tenantId != null)
        loginQuery = loginQuery.Where(lg => lg.TenantId == tenantId.Value);
      var logins = loginQuery.ToList(); 
      if(logins.Count == 0)
        return; //it might happen if user name is not known
      //Suspend login
      var suspendedUntil = utcNow.Add(Settings.SuspensionPeriod);
      var loginLogService = _app.GetService<ILoginLogService>();
      var msg = StringHelper.SafeFormat("{0} login failures. Login {1} suspended until {2}", failCount, failedUserName, suspendedUntil);
      var loginModule = _app.GetModule<LoginModule>();
      foreach(var lg in logins) {
        // if already suspended or disabled, do not suspend again
        if(lg.Flags.IsSet(LoginFlags.Disabled | LoginFlags.Suspended))
          continue; 
        lg.Flags |= LoginFlags.Suspended;
        lg.SuspendedUntil = suspendedUntil;
        //raise event - it will log to login log
        loginModule.OnLoginEvent(session.Context, LoginEventType.LoginSuspended, lg, msg, failedUserName);
      }
      session.SaveChanges();
      var incService = _app.GetService<IIncidentLogService>();
      incService.LogIncident(LoginModule.LoginIncidentType, msg, LoginEventType.LoginSuspended.ToString(), key1: failedUserName, keyId2: tenantId);
    }

  }//class

}
