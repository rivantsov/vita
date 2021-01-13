using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Login {


  /// <summary>Describes user login options.</summary>
  [Flags]
  public enum LoginFlags {
    None = 0x00,
    /// <summary>Login is disabled permanently.</summary>
    Disabled = 1, 
    /// <summary>Login is suspended temporarily.</summary>
    Suspended = 1 << 1,
    /// <summary>The password expired, login is suspended.</summary>
    PasswordExpired = 1 << 2,

    /// <summary>Indicates that multi-factor login process is required for this user. </summary>
    RequireMultiFactor = 1 << 4,
    /// <summary>Indicates that system does not need to hide the fact that a user is signed up with the system.
    /// Typical for business systems. </summary>
    DoNotConcealMembership = 1 << 5,

    /// <summary>Indicates that the password on the account is temporary can be used only once. 
    /// The system should require changing the password immediately after login.</summary>
    OneTimePassword = 1 << 9,
    /// <summary>Indicates that one-time password had been already used.</summary>
    OneTimePasswordUsed = 1 << 10, 

    Inactive = Disabled | Suspended | PasswordExpired,
  }

  [Flags]
  public enum SecretQuestionFlags {
    None = 0,
    Disabled = 1,
    Private = 1 << 1, // When created through "Write my own question"
    Extra = 1 << 2, // not showing by default
  }

  public enum DeviceTrustLevel {
    None,
    AllowSingleFactor, // one-factor identification is enough
  }

  public enum LoginProcessType {
    PasswordReset,  // resetting password
    FactorVerification, // email or phone verification
    MultiFactorLogin, // multi-factor login process
  }

  [Flags]
  public enum ExtraFactorTypes {
    None = 0,
    Email = 1,
    Phone = 1 << 1,
    SecretQuestions = 1 << 2,
    GoogleAuthenticator = 1 << 3, 
  }


  public enum LoginAttemptStatus {
    Failed,
    AccountInactive,
    AccountSuspended,
    PendingMultifactor,
    Success,
  }

  [Flags]
  public enum PostLoginActions {
    None = 0,
    WarnPasswordExpires = 1,
    ForceChangePassword = 1 << 1,
    SetupExtraFactors = 1 << 3, 
  }

  public enum LoginProcessStatus {
    Active,
    Completed,
    Expired,
    AbortedAsFraud,
  }

  [Flags]
  public enum LoginProcessFlags {
    None = 0,
    Obscured = 1, // user did not confirm any factors, so do not disclose to UI if user exist or not in the database
  }

  public enum DeviceType {
    Unknown = 0,
    Computer = 1,
    Tablet = 2, 
    Phone = 3,
    Custom = 5,
  }

  /// <summary>Event types for LoginLog, LoginEvent and IncidentLog. </summary>
  public enum LoginEventType {
    // Login/logout events
    Login,
    Logout,
    LoginPendingMultiFactor,
    LoginFailed,
    LoginSuspended,
    LoginDisabled,
    PasswordExpired,
    LoginReactivated,
    TokenRefreshed,

    //management
    LoginCreated,
    LoginChanged,
    PasswordChanged,
    OneTimePasswordSet,
    SecretQuestionChanged,
    TrustedDeviceChanged,

    //Processes
    MultiFactorLoginStarted,
    MultiFactorLoginCompleted,
    PasswordResetStarted,
    FactorVerificationStarted,
    PinSent,
    PinMismatch,
    PinMatched,
    QuestionAnswersFailed,
    QuestionAnswersSucceeded,
    ProcessAborted,
  }

}
