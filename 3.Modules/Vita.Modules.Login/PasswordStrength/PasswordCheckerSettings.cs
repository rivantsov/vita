using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Login {

  [Flags]
  public enum CharOptions {
    None = 0,
    Letter = 1,
    MixedCase = 1 << 1,
    Digit = 1 << 2,
    SpecialChar = 1 << 3,
  }

  public class StrengthLevelData {
    public PasswordStrength Strength;
    public int MinLength;
    public int MinDistinctLength;
    public CharOptions Options;

    public bool Conforms(int length, int distinctCount, CharOptions options) {
      return length >= MinLength && distinctCount >= MinDistinctLength &&
        (Options & options) == Options; // all required option flags are present in 'options'
    }
  }

  public class PasswordCheckerSettings {
    public string Description;
    public int MinLength = 6;
    public IList<StrengthLevelData> StrengthLevels = new List<StrengthLevelData>();  
    public HashSet<string> ForbiddenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static PasswordCheckerSettings CreateDefault() {
      var stt = new PasswordCheckerSettings();
      stt.Description = @"Password must be at least 10 characters long with at least 5 distinct charactres,
and must contain at least one of the each of the following character types: upper-case letter, 
lower-case letter, digit, special symbol.";
      stt.ForbiddenTokens.UnionWith(new string[] { "password", "1234", "4321", "abcd", "xyz", "qwert", "asdf" });
      stt.StrengthLevels.Add(new StrengthLevelData() { Strength = PasswordStrength.Strong, MinLength = 10, MinDistinctLength = 5,
        Options = CharOptions.Letter | CharOptions.MixedCase | CharOptions.Digit | CharOptions.SpecialChar
      });
      stt.StrengthLevels.Add(new StrengthLevelData() {Strength = PasswordStrength.Medium, MinLength = 8, MinDistinctLength = 4,
        Options = CharOptions.Letter | CharOptions.Digit
      });
      return stt;
    }
  }

}//ns
