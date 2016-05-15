using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Login {
  public static class LoginNotificationTypes {
    public const string PasswordResetPin = "Login.PasswordReset.Pin";
    public const string FactorVerifyPin = "Login.FactorVerify.Pin";
    public const string MultiFactorPin = "Login.MultiFactor.Pin";
    public const string OneTimePassword = "Login.OneTimePassword";
    public const string PasswordReset = "Login.PasswordReset";
  }

  public static class LoginNotificationKeys {
    // Data Keys - used for formatting email templates
    public const string UserName = "UserName";
    public const string BackHitUrlBase = "BackHitUrlBase"; // back hit URL for link in email
    public const string ProcessToken = "ProcessToken";
    public const string Pin = "Pin";
    public const string OneTimePassword = "OneTimePassword";
  }

  // Template names - must exist in TextTemplate table if you use Smtp service with TextTemplates module for sending notifications
  // This class has 'old' constant names and new constant values (v 1.1, Feb 2016)
  public static class LoginMessageTemplates {
    // Password reset
    public const string PasswordResetPinEmailSubject = "Login.PasswordReset.Pin.Email.Subject";
    public const string PasswordResetPinEmailBody = "Login.PasswordReset.Pin.Email.Body";
    public const string PasswordResetPinSmsBody = "Login.PasswordReset.Pin.Sms.Body";
    public const string PasswordResetCompleteEmailSubject = "Login.PasswordReset.Email.Subject";
    public const string PasswordResetCompleteEmailBody = "Login.PasswordReset.Email.Body";
    //Factor verification
    public const string VerifyEmailSubject = "Login.FactorVerify.Pin.Email.Subject";
    public const string VerifyEmailBody = "Login.FactorVerify.Pin.Email.Body";
    public const string VerifySmsBody = "Login.FactorVerify.Pin.Sms.Body";
    // Multifactor login
    public const string MultiFactorEmailSubject = "Login.MultiFactor.Pin.Email.Subject";// !!!!!
    public const string MultiFactorEmailBody = "Login.MultiFactor.Pin.Email.Body";
    public const string MultiFactorSmsBody = "Login.MultiFactor.Pin.Sms.Body";
    // One-time password
    public const string OneTimePasswordSubject = "Login.OneTimePassword.Email.Subject";
    public const string OneTimePasswordBody = "Login.OneTimePassword.Email.Body";

    [Obsolete("Use VerifySmsBody constant.")]
    public const string VerifyPhoneSmsBody = VerifySmsBody;
  }




}
