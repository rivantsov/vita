using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Modules.Login.Api {
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



}
