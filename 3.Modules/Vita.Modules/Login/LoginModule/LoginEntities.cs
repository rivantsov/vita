using System;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Modules.EncryptedData;
using Vita.Modules.Logging;

namespace Vita.Modules.Login {


  [Entity]
  [Index("UserNameHash,WeakPasswordHash,TenantId")]
  public interface ILogin {
    [PrimaryKey, Auto]
    Guid Id { get; set; } 
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }
    
    //should be nullable, but using Guid.Empty for null is a bit more efficient
    Guid TenantId { get; set; }

    //Plain-text userName; 
    string UserName { get; set; }
    [HashFor("UserName"), Index]
    int UserNameHash { get; } //simple hash to speed-up lookup

    [Index]
    Guid UserId { get; set; }
    [Index]
    Int64 AltUserId { get; set; }

    [Size(LoginModuleSettings.MaxHashSize)] 
    string PasswordHash { get; set; }
    int HashWorkFactor { get; set; } //BCrypt work factor, or iteration count for other algorithms
    int WeakPasswordHash { get; set; } //one-byte hash, to rule out bogus login attempts in attack

    LoginFlags Flags { get; set; }
    [Utc]
    DateTime? LastLoggedInOn { get; set; }
    [Utc]
    DateTime? Expires { get; set; }
    [Utc]
    DateTime? PasswordResetOn { get; set; } //created or reset
    [Utc]
    DateTime? SuspendedUntil { get; set; }

    // extra factors to use in multi-factor process
    ExtraFactorTypes PasswordResetFactors { get; set; }
    ExtraFactorTypes MultiFactorLoginFactors { get; set; }
    ExtraFactorTypes IncompleteFactors { get; set; }

    IList<ISecretQuestionAnswer> SecretQuestionAnswers { get; }
    IList<ILoginExtraFactor> ExtraFactors { get; set; }
  }


  [Entity, OrderBy("Number")]
  public interface ISecretQuestion {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(Sizes.Description)]
    string Question { get; set; }

    int Number { get; set; } //order to show in drop-downs for user to select

    SecretQuestionFlags Flags { get; set; } 
  }

  [Entity, PrimaryKey("Login,Question"), ClusteredIndex("Login,Question"), OrderBy("Number")]
  public interface ISecretQuestionAnswer {
    ILogin Login { get; set; }
    ISecretQuestion Question { get; set; }
    int Number { get; set; }
    int AnswerHash { get; set; }
  }

  
  [Entity, ClusteredIndex("Login,Id")]
  public interface ITrustedDevice {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }

    ILogin Login { get; set; }

    DeviceType Type { get; set; }
    DeviceTrustLevel TrustLevel { get; set; }

    [Size(100)]
    string Token { get; set; } //free-form identifier, browser cookie
    [Utc]
    DateTime LastLoggedIn { get; set; }
  }

  [Entity, ClusteredIndex("Login,Id")]
  public interface ILoginExtraFactor {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }

    ILogin Login { get; set; }
    ExtraFactorTypes FactorType { get; set; }
    IEncryptedData Info { get; set; } // phone or email or whatever
    [Index]
    int InfoHash { get; set; } // stable hash for info, for quick search
    DateTime? VerifiedOn { get; set; }
  }


  /// <summary>Password history holds weak hashes of previous passwords, 
  /// to prevent reusing recent passwords (if set by policy).
  /// </summary>
  [Entity]
  public interface IPasswordHistory {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }
    ILogin Login { get; set; }
    int WeakPasswordHash { get; set; }
    DateTime? DisabledOn { get; set; }
  }

  //Used for controlling multi-step processes like 2-Factor login, or password reset.
  [Entity, DoNotTrack]
  public interface ILoginProcess {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(100)]
    string Token { get; set; } //randomly generated token
    [HashFor("Token")]
    int TokenHash { get; set; }

    [Auto(AutoType.CreatedOn), Utc]
    DateTime CreatedOn { get; set; }
    [Utc]
    DateTime ExpiresOn { get; set; }

    LoginProcessStatus Status { get; set; }
    LoginProcessFlags Flags { get; set; }

    [Nullable]
    ILogin Login { get; set; }

    LoginProcessType ProcessType { get; set; }

    ExtraFactorTypes CompletedFactors { get; set; }
    ExtraFactorTypes PendingFactors { get; set; }

    [Nullable, Size(Sizes.Description)]
    string Notes { get; set; }

    [Nullable]
    ILoginExtraFactor CurrentFactor { get; set; }

    [Size(100), Nullable]
    string CurrentPin { get; set; }

    [Size(100), Nullable]
    string AnsweredQuestions { get; set; }

    int FailCount { get; set; }

    Guid? WebCallId { get; set; }
  }


}
