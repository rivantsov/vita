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
    /// <summary>RefreshToken, used for refreshing user session token.</summary>
    public string RefreshToken;
    /// <summary>User display name</summary>
    public string UserDisplayName;
    /// <summary>For multi-factor login, the token identifying the server-side process that controls the multi-factor verification.</summary>
    public string MultiFactorProcessToken;
  }


  /// <summary>Secret question data.</summary>
  public class SecretQuestion {
    /// <summary>Question ID.</summary>
    public Guid Id;
    /// <summary>Question.</summary>
    public string Question;
  }

  /// <summary>A container for an answer to secret question.</summary>
  public class SecretQuestionAnswer {
    /// <summary>Question ID.</summary>
    public Guid QuestionId;
    /// <summary>User answer.</summary>
    public string Answer;
  }

  /// <summary>Contains user login information.</summary>
  public class LoginInfo {
    /// <summary>Login record ID. Usually the same as user ID.</summary>
    public Guid Id;
    /// <summary>Tenant ID for multi-tenant environments.</summary>
    public Guid TenantId;
    /// <summary>User name.</summary>
    public string UserName;
    /// <summary>Date-time when user was last logged in.</summary>
    public DateTime? LastLoggedInOn;
    /// <summary>Date-time when the password expires.</summary>
    public DateTime? Expires;
    /// <summary>For a suspended account contains date-time of the end of suspension. 
    /// Typically used when suspending account for a short time after several failed login
    /// attempts. </summary>
    public DateTime? SuspendedUntil;
    /// <summary>The types of extra factors (email, phone) required for 
    /// password reset. </summary>
    public ExtraFactorTypes PasswordResetFactors;
    /// <summary>The types of extra factors required to confirm for multi-factor login.</summary>
    public ExtraFactorTypes MultiFactorLoginFactors;
    /// <summary>A list of extra login factors that are not yet confirmed.</summary>
    public ExtraFactorTypes IncompleteFactors;
    /// <summary>Login flags.</summary>
    public LoginFlags Flags; 
  }

  /// <summary>The information about login extra factor: email, phone.</summary>
  public class LoginExtraFactor {
    /// <summary>Factor ID.</summary>
    public Guid Id;
    /// <summary>Factor type.</summary>
    public ExtraFactorTypes Type;
    /// <summary>Indicates if the factor was verified. Ex: email ownership was confirmed by sending pin.</summary>
    public bool Confirmed;
    /// <summary>The factor value, for ex: email address.</summary>
    public string Value; 
  }

  /// <summary>Data to start the password reset process.</summary>
  public class PasswordResetStartRequest {
    /// <summary>Value of captcha (if used).</summary>
    public string Captcha;
    /// <summary>The first extra authentication factor to start process, usually email or phone number. </summary>
    public string Factor;
  }

  /// <summary>Information for sending secret pin through authentication factor/channel.</summary>
  public class SendPinRequest {
    /// <summary>Login/password reset process token. </summary>
    public string ProcessToken;
    /// <summary>Factor type (email, phone). Used only in multi-factor login. Ignored in password reset process call. </summary>
    public ExtraFactorTypes FactorType;
    /// <summary>The factor value (email, phone). Used only in password reset process. Ignored in multi-factor login process. </summary>
    public string Factor;
  }


  /// <summary>Pin verification data.</summary>
  public class VerifyPinRequest {
    /// <summary>Process token. </summary>
    public string ProcessToken;
    /// <summary>Pin value. </summary>
    public string Pin;
  }


  /// <summary>The password change information.</summary>
  public class PasswordChangeInfo {
    /// <summary>Old password; required only for direct password changed, when user is logged in and 
    /// wants to changes his/her password. Not used in password reset process (forgot password). </summary>
    public string OldPassword; 
    /// <summary>A new password.</summary>
    public string NewPassword;
  }

  /// <summary>Contains information about one-time temporary password.</summary>
  public class OneTimePasswordInfo {
    /// <summary>The password.</summary>
    public string Password;
    /// <summary>Password expiration, hours.</summary>
    public int ExpiresHours; 
  }

  /// <summary>Describes a login process, for ex: password reset process.</summary>
  public class LoginProcess {
    /// <summary>Process token identifying the process in API calls.</summary>
    public string Token;
    /// <summary>The process type.</summary>
    public LoginProcessType ProcessType;
    /// <summary>Completed factors.</summary>
    public ExtraFactorTypes CompletedFactors;
    /// <summary>Pending factors to be completed.</summary>
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

  public class MultifactorLoginRequest {
    public string ProcessToken { get; set; }
    public UserSessionExpirationType Expiration { get; set; }
  }

}
