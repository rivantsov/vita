using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Login.Api {

  public static class LoginModelExtensions {
    public static LoginProcess ToModel( this ILoginProcess process) {
      if(process == null)
        return null; 
      return new LoginProcess() {
        Token = process.Token, CompletedFactors = process.CompletedFactors, PendingFactors = process.PendingFactors
      };      
    }

    public static SecretQuestion ToModel(this ISecretQuestion question) {
      return new SecretQuestion() {Id = question.Id, Question = question.Question};
    }

    public static LoginInfo ToModel(this ILogin login) {
      if (login == null)
        return null; 
      var flags = login.Flags;
      var result = new LoginInfo() {
        Id = login.Id, UserName = login.UserName, 
        Expires = login.Expires,   Flags = login.Flags,
        MultiFactorLoginFactors = login.MultiFactorLoginFactors,
        PasswordResetFactors = login.PasswordResetFactors, IncompleteFactors = login.IncompleteFactors,
        LastLoggedInOn = login.LastLoggedInOn, SuspendedUntil = login.SuspendedUntil
      };
      //If suspension expired, fix the result; we do not fix login entity - it will be cleared when user logs in; 
      // the trouble here is that we might not have permissions to update login at this moment, only read it
      if(login.Flags.IsSet(LoginFlags.Suspended)) {
        var utcNow = EntityHelper.GetSession(login).Context.App.TimeService.UtcNow;
        if(login.SuspendedUntil < utcNow) {
          result.Flags &= ~LoginFlags.Suspended;
          result.SuspendedUntil = null; 
        }
      }
      return result; 
    }

  }//class
}//ns
