using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Modules.Login.GoogleAuthenticator {
  // based on code from here: http://www.codeproject.com/Articles/403355/Implementing-Two-Factor-Authentication-in-ASP-NET

  public static class GoogleAuthenticatorUtil {
    public const int AuthenticatorSecretLength = 10; 
    public static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static string GenerateSecret() {
      return Base32Encoder.Encode(RandomHelper.GenerateRandomBytes(AuthenticatorSecretLength));
    }
    public static string GetQRUrl(string identity, string secret) {
      const string GoogleQrUrlTemplage = "https://www.google.com/chart?chs=200x200&chld=M|0&cht=qr&chl=otpauth://totp/{0}%3Fsecret%3D{1}";
      return StringHelper.FormatUri(GoogleQrUrlTemplage, identity, secret);
    }

    public static bool CheckPasscode(string secret, string passcode) {
      var bytes = Base32Encoder.Decode(secret); 
      return CheckPasscode(bytes, passcode);
    }

    public static bool CheckPasscode(byte[] secret, string passcode) {
      var shifts = new[] { 0, 1, -1, 2, -2, 3 }; //up to 90 seconds before/after
      var currCounter = GetCurrentCounter();
      for (int i = 0; i < shifts.Length; i++) {
        var current = GeneratePasscode(secret, currCounter + shifts[i]);
        if (passcode == current)
          return true;
      }
      return false;
    }

    public static string GeneratePasscode(string secret) {
      var bytes = Base32Encoder.Decode(secret); 
      long counter = GetCurrentCounter(); 
      return GeneratePasscode(bytes, counter);
    }

    public static long GetCurrentCounter() {
      return (long)(DateTime.UtcNow - UNIX_EPOCH).TotalSeconds / 30;
    }

    public static string GeneratePasscode(byte[] secret, long iterationNumber, int digits = 6) {
      byte[] counter = BitConverter.GetBytes(iterationNumber);

      if (BitConverter.IsLittleEndian)
        Array.Reverse(counter);

      HMACSHA1 hmac = new HMACSHA1(secret);

      byte[] hash = hmac.ComputeHash(counter);

      int offset = hash[hash.Length - 1] & 0xf;

      int binary =
          ((hash[offset] & 0x7f) << 24)
          | ((hash[offset + 1] & 0xff) << 16)
          | ((hash[offset + 2] & 0xff) << 8)
          | (hash[offset + 3] & 0xff);

      int password = binary % (int)Math.Pow(10, digits); // 6 digits

      return password.ToString(new string('0', digits));
    }

  }//class
}//ns
