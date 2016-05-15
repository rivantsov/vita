using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.Login.Api {

  public class LoginRequest {
    public string UserName;
    public string Password;
    public string DeviceToken;
    public Guid? TenantId;
  }

  public class LoginResponse {
    public LoginAttemptStatus Status;
    public string UserName;
    public Guid UserId;
    public long AltUserId;
    public Guid LoginId;
    public DateTime? LastLoggedInOn;
    public PostLoginActions Actions; 
    public int PasswordExpiresDays;
    public string AuthenticationToken; //the client app should put this token into Authorization header on each subsequent request
    public string UserDisplayName;
    public string MultiFactorProcessToken;
  }

  public class SecretQuestion {
    public Guid Id;
    public string Question;
  }

  public class SecretQuestionAnswer {
    public Guid QuestionId;
    public string Answer;
  }

  public class LoginInfo {
    public Guid Id;
    public Guid TenantId;
    public string UserName;
    public DateTime? LastLoggedInOn;
    public DateTime? Expires;
    public DateTime? SuspendedUntil;
    public ExtraFactorTypes PasswordResetFactors;
    public ExtraFactorTypes MultiFactorLoginFactors;
    public ExtraFactorTypes IncompleteFactors;
    public LoginFlags Flags; 
    //Deprecated, use Flags enumeration instead
    public bool OneTimePassword;
    public bool RequireMultiFactorLogin;
    public bool Disabled;
    public bool Suspended;
    // Value of 'do not conceal membership' flag in login. User is business user and his membership is not a secret
    public bool DoNotConcealMembership;
  }

  public class LoginExtraFactor {
    public Guid Id;
    public ExtraFactorTypes Type; //Email or Phone
    public bool Confirmed;
    public string Value; 
  }


  public class PasswordResetStartRequest {
    public string Captcha;
    public string Factor;
  }

  public class SendPinRequest {
    public string Factor; 
  }

  public class PasswordChangeInfo {
    public string OldPassword;
    public string NewPassword;
  }

  public class OneTimePasswordInfo {
    public string Password;
    public int ExpiresHours; 
  }

  public class LoginProcess {
    public string Token;
    public LoginProcessType ProcessType;
    public ExtraFactorTypes CompletedFactors;
    public ExtraFactorTypes PendingFactors;
  }


  public class LoginSearch : SearchParams {
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? ExpiringBefore { get; set; }
    public bool EnabledOnly { get; set; }
    public bool SuspendedOnly { get; set; }
  }

  public class DeviceInfo {
    public string Token;
    public DeviceType Type; 
    public DeviceTrustLevel TrustLevel; 
  }

  public class SessionInfo {
    public Guid UserId;
    public UserKind Kind;
    public string UserName;
    public string Culture;
    public int TimeZoneOffsetMinutes;
    public DateTime StartedOn;
  }

}
