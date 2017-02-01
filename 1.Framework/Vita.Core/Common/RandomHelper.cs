using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {

  [Flags]
  public enum CharKinds {
    None = 0,
    Digit = 1,
    UpperLetter = 1 << 1,
    LowerLetter = 1 << 2,
    SpecialChar = 1 << 3,

  }
  
  public static class RandomHelper {
    static CharKinds[] _allKinds = new[] {
      CharKinds.Digit, CharKinds.UpperLetter, CharKinds.LowerLetter, CharKinds.SpecialChar };
    static string _specialChars = "~!@#$%^&*-=+?";

    public static byte[] GenerateRandomBytes(int len) {
      var gen = RandomNumberGenerator.Create();
      var bytes = new byte[len];
      gen.GetBytes(bytes);
      return bytes; 
    }

    public static string GenerateRandomString(int length, CharKinds kinds = CharKinds.UpperLetter | CharKinds.Digit) {
      Util.Check(kinds != CharKinds.None, "CharKinds may not be empty");
      var rnd = new Random();
      var chars = new char[length];
      for(int i = 0; i < length; i++) 
        chars[i] = GenerateRandomChar(rnd, kinds);
      return new String(chars);
    }

    public static bool IsSet(this CharKinds kinds, CharKinds kind) {
      return (kinds & kind) != 0;
    }

    private static char GenerateRandomChar(Random random, CharKinds kinds) {
      var kind = GetRandomKind(random, kinds);
      switch(kind) {
        case CharKinds.Digit: return (char)('0' + random.Next(10));
        case CharKinds.UpperLetter: return (char)('A' + random.Next(26));
        case CharKinds.LowerLetter: return (char)('a' + random.Next(26));
        case CharKinds.SpecialChar: 
        default:
          return _specialChars[random.Next(_specialChars.Length)];
      }
    }

    private static CharKinds GetRandomKind(Random random, CharKinds kinds) {
      Util.Check(kinds != CharKinds.None, "Char kinds may not be empty.");
      // if it is a single kind, just return it. 
      switch(kinds) {
        case CharKinds.UpperLetter: case CharKinds.LowerLetter: 
        case CharKinds.Digit: 
        case CharKinds.SpecialChar:
          return kinds; 
      }
      // if more than one, throw dice 
      while(true) {
        var kind = _allKinds[random.Next(_allKinds.Length)];
        if(kinds.IsSet(kind))
          return kind; 
      }
    }

    public static void RandomizeListOrder<T>(IList<T> list) {
      var copy = new List<T>(list); 
      var rand = new Random(); 
      list.Clear();
      while (copy.Count > 0) {
        var randIndex = rand.Next(copy.Count);
        var elem = copy[randIndex];
        copy.RemoveAt(randIndex);
        list.Add(elem);
      }
    }

    // Chars with excluded similar chars
    private static char[] _safeLetters = "ABCDEFGHJKMNPQRSTUVWXYZ".ToCharArray();
    private static char[] _safeDigits = "23456789".ToCharArray();
    private static char[] _safeAlpha = "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToCharArray();


    public static string GenerateSafeRandomWord(int len = 10) {
      var rand = new Random();
      var pin = GetRandomString(rand, len, _safeAlpha);
      return pin;
    }

    public static string GenerateRandomNumber(int len = 5) {
      var rand = new Random();
      var pin = GetRandomString(rand, len, _safeDigits);
      return pin;
    }

    private static string GetRandomString(Random rand, int length, char[] fromChars) {
      var chars = new char[length];
      for(int i = 0; i < length; i++)
        chars[i] = fromChars[rand.Next(fromChars.Length)];
      return new string(chars);
    }


  }//class
}
