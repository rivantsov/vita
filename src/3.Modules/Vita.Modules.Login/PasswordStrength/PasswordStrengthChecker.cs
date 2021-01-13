using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 

namespace Vita.Modules.Login {
  
  public class PasswordStrengthChecker : IPasswordStrengthChecker {
    PasswordCheckerSettings _settings; 
    public PasswordStrengthChecker(EntityApp app, PasswordCheckerSettings settings = null) {
      _settings = settings ?? PasswordCheckerSettings.CreateDefault();
      app.RegisterConfig(_settings); 
    }

    public string GetDescription() {
      return _settings.Description; 
    }

    public PasswordStrength Evaluate(string password) {
      if(password == null)
        return PasswordStrength.Unacceptable;
      password = password.Trim(); 
      var length = password.Length;
      if (password.Contains(' ') || length < _settings.MinLength)
        return PasswordStrength.Unacceptable;
      // check forbidden
      if(_settings.ForbiddenTokens.Any(t => password.Contains(t)))
        return PasswordStrength.Unacceptable;  
      var passwordChars = password.ToCharArray(); 
      var distinctCount = new HashSet<char>(passwordChars).Count;
      var charOptions = GetCharOptions(passwordChars);
      // Check strong, medium
      if(ConformsLevel(PasswordStrength.Strong, length, distinctCount, charOptions))
        return PasswordStrength.Strong;
      if(ConformsLevel(PasswordStrength.Medium, length, distinctCount, charOptions))
        return PasswordStrength.Medium; 
      return PasswordStrength.Weak;       
    }

    private bool ConformsLevel(PasswordStrength strength, int length, int distinctCount, CharOptions charOptions) {
      StrengthLevelData levelStt = _settings.StrengthLevels.FirstOrDefault(lvl => lvl.Strength == strength); 
      if (levelStt != null &&  levelStt.Conforms(length, distinctCount, charOptions))
        return true;
      return false; 
    }

    public string GenerateStrongPassword() {
      var safeDigits = "23456789";
      var safeLetters = "ABCDEFGHJKMNPQRSTUVWXYZ";
      var safeSpecChars = "?&#%$";
      var safeLettersAndDigits = safeDigits + safeLetters;
      var strongStt = _settings.StrengthLevels.FirstOrDefault(lvl => lvl.Strength == PasswordStrength.Strong);
      if (strongStt == null)
        strongStt = new StrengthLevelData() { Strength = PasswordStrength.Strong, MinLength = 10, 
            Options = CharOptions.Digit | CharOptions.Letter | CharOptions.SpecialChar | CharOptions.MixedCase };
      string pwd = string.Empty;
      var rand = new Random();
      while (pwd.Length < strongStt.MinLength) {
        pwd += safeLetters[rand.Next(safeLetters.Length)];
        pwd += safeDigits[rand.Next(safeDigits.Length)];
      }
      //Add mixed case and special char if specified
      if (strongStt.Options.IsSet(CharOptions.MixedCase))
        pwd += char.ToLowerInvariant(safeLetters[rand.Next(safeLetters.Length)]);
      if (strongStt.Options.IsSet(CharOptions.SpecialChar))
        pwd += safeSpecChars[rand.Next(safeSpecChars.Length)];
      return pwd; 
    }

    private static CharOptions GetCharOptions(char[] chars) {
      var hasLower = false;
      var hasUpper = false;
      var result = CharOptions.None;
      foreach(var ch in chars) {
        if(char.IsLetter(ch)) {
          result |= CharOptions.Letter;
          if(char.IsUpper(ch)) hasUpper = true;
          if(char.IsLower(ch)) hasLower = true;
        } else if(char.IsDigit(ch))
          result |= CharOptions.Digit;
        else
          result |= CharOptions.SpecialChar;
      }//foreach
      if(hasLower && hasUpper)
        result |= CharOptions.MixedCase;
      return result; 
    }//method

  }//class

} //ns
