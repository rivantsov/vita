using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.Login {

  [Flags]
  public enum LoginModuleOptions {
    None = 0,

    // Hide membership - when user tries to reset password and enters email, do not disclose in api controllers if API is found or not.
    ConcealMembership = 1,

    RequireCaptchaOnNewLogin = 1 << 1,
    AskOldPasswordOnChange = 1 << 3,
    /// <summary>Instructs the system to generate simple temporary passwords, not necessarily strong ones. Example: AB7326 </summary>
    GenerateSimpleTempPasswords = 1 << 4,
    AllowPasswordResetOnSuspended = 1 << 5,

    Default = ConcealMembership | AskOldPasswordOnChange | AllowPasswordResetOnSuspended | GenerateSimpleTempPasswords,
  }

  public class LoginModuleSettings {
    public const int MaxHashSize = 250; //size of string representation of password hash in database 

    public readonly LoginModuleOptions Options;
    public string GoogleAuthenticatorIdentityTemplate = "{0}:{1}"; // AppName:username, will be displayed in GoogleAuthenticator app for an account
    public readonly IPasswordHasher PasswordHasher;
    public TimeSpan? PasswordExpirationPeriod;
    public int WarnPasswordExpiresDays = 7;
    public ExtraFactorTypes DefaultPasswordResetFactors = ExtraFactorTypes.Email | ExtraFactorTypes.SecretQuestions;  //default extra factors
    public int MinQuestionsCount = 3; //minimum count of secret questions
    public TimeSpan OneTimePasswordExpiration = TimeSpan.FromHours(24);
    public int MaxFailCount = 5; //max number of failures (wrong pin) after which process is disabled

    public string DefaultEmailFrom = null;

    public Func<ILoginProcess, ILoginExtraFactor, string> PinGenerator = LoginModule.DefaultGeneratePin;  //default generator, replace with your own if you need to
    // Suspend account on multiple failures
    public int SuspendOnFailCount = 3;   // # of incidents to trigger suspension
    public int SuspendOnFailMinutes = 10;


    //Expiration periods for processes
    public TimeSpan PasswordResetTimeWindow = TimeSpan.FromMinutes(5);
    public TimeSpan FactorVerificationTimeWindow = TimeSpan.FromHours(12);
    public TimeSpan MultiFactorLoginTimeWindow = TimeSpan.FromMinutes(2);

    //Password strength
    public IPasswordStrengthChecker PasswordChecker;
    public PasswordStrength RequiredPasswordStrength = PasswordStrength.Medium;

    //Login interceptor - external method to allow login after all had been verified
    public Func<OperationContext, ILogin, LoginAttemptStatus, LoginAttemptStatus> CheckCanLoginFunc;

    // Provided by external code, enables sending messages to users (pwd reset, pins, etc)
    public ILoginMessagingService MessagingService; 

    public LoginModuleSettings(
                 LoginModuleOptions options = LoginModuleOptions.Default,
                 ILoginMessagingService messagingService = null,
                 TimeSpan? passwordExpirationPeriod = null,
                 IPasswordHasher passwordHasher = null, 
                 int bcryptWorkFactor = 10) {
      Options = options;
      MessagingService = messagingService; 
      PasswordExpirationPeriod = passwordExpirationPeriod;
      PasswordHasher = passwordHasher ?? new BCryptPasswordHasher(bcryptWorkFactor);
    }
  }//class

}//ns
