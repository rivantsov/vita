using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Login {

  public enum PasswordStrength {
    Unacceptable,
    Weak,
    Medium,
    Strong,
  }
  public interface IPasswordStrengthChecker {
    string GetDescription();
    PasswordStrength Evaluate(string password);
    string GenerateStrongPassword();

  }
}
