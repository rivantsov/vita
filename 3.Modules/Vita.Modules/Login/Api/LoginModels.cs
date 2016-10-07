using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Modules.Login.Api {

  /// <summary>A container for login information. </summary>
  public class LoginRequest {
    /// <summary>User name.</summary>
    public string UserName;
    /// <summary>User password.</summary>
    public string Password;
    /// <summary>Requested session expiration type, optional.</summary>
    public UserSessionExpirationType? ExpirationType; 
    /// <summary>A token identifying the device (client computer or device). Obtained by registering device with server. 
    /// Allows implementing optional behavior like [Skip multi-factor authorization] for pinned trusted device. </summary>
    public string DeviceToken;
    /// <summary>A tenant ID for multi-tenant applications. </summary>
    public Guid? TenantId;
  }

  /// <summary>Login attempt response. </summary>
  public class LoginResponse {
    /// <summary>Login attempt result status. Indicates if login succeeded or failed. </summary>
    public LoginAttemptStatus Status;
    /// <summary>User name (if login succeeded).</summary>
    public string UserName;
    /// <summary>User ID (if login succeeded).</summary>
    public Guid UserId;
    /// <summary>Alternative user ID, Int63 (if login succeeded).</summary>
    public long AltUserId;
    /// <summary>Login record ID (if login succeeded).</summary>
    public Guid LoginId;
    /// <summary>Date-time of previous successful login.</summary>
    public DateTime? LastLoggedInOn;
    /// <summary>List of pending actions that user should complete with the account setup (ex: verify email).</summary>
    public PostLoginActions Actions;
    /// <summary>Number of days the current password expires.</summary>
    public int PasswordExpiresDays;
    /// <summary>Authentication token, serving as user session ID. Should be added to Authorization header of every call to the server.</summary>
    public string AuthenticationToken; 
    /// <summary>User display name</summary>
    public string UserDisplayName;
    /// <summary>For multi-factor login, the token identifying the server-side process that controls the multi-factor verification.</summary>
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

}
