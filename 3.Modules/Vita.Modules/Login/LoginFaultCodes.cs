using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Login {
  public static class LoginFaultCodes {
    public const string UserNameTooShort = "UserNameTooShort";
    public const string LoginDisabled = "LoginDisabled";
    public const string WeakPassword = "WeakPassword";
  }
}
