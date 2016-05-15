using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Vita.Common; 

namespace Vita.Modules.ApiClients.Recaptcha {
  // based on RecapchaV2.NET:  https://github.com/xantari/RecaptchaV2.NET

  public static class RecaptchaUtil {

    public static string GenerateSecureToken(Guid sessionId, long timeStamp, byte[] keyIv) {
      var json = "{" + string.Format("\"session_id\": {0},\"ts_ms\":{1}", sessionId, timeStamp) + "}";
      var bytes = EncryptSecureToken(json, keyIv);
      var base64 = Convert.ToBase64String(bytes);
      var escaped = StringHelper.EscapeForUri(base64);
      return escaped; 
    }

    public static byte[] EncryptSecureToken(string tokenJson, byte[] keyIv) {
      // Create an AesManaged object 
      // with the specified key and IV. 
      using (AesManaged aes = new AesManaged()) {
        //aes.Key = keyIv;  
        //aes.IV = keyIv; 
        aes.Padding = PaddingMode.PKCS7; 
        aes.Mode = CipherMode.ECB;
        ICryptoTransform encryptor = aes.CreateEncryptor(keyIv, keyIv);
        using (MemoryStream stream = new MemoryStream()) {
          using (CryptoStream csEncrypt = new CryptoStream(stream, encryptor, CryptoStreamMode.Write)) {
            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt)) {
              swEncrypt.Write(tokenJson);
            }
            return stream.ToArray();
          }
        }//using stream
      }
    }//method

    /// <summary>
    /// Gets the first 16 bytes of the SHA1 version of the SecretKey. (This is not documented ANYWHERE on googles dev site, you have to READ the java code to figure this out!!!!)
    /// </summary>
    /// <param name="siteSecret">Googles recaptcha site secret.</param>
    /// <returns>First 16 bytes of the SHA1 hash of the secret.</returns>
    public static byte[] GetEncryptionKey(string siteSecret) {
      SHA1 sha = SHA1.Create();
      byte[] dataToHash = Encoding.UTF8.GetBytes(siteSecret);
      byte[] shaHash = sha.ComputeHash(dataToHash);
      byte[] first16OfHash = new byte[16];
      Array.Copy(shaHash, first16OfHash, 16);
      return first16OfHash;
    }

    public static bool IsSet(this RecaptchaOptions options, RecaptchaOptions option) {
      return (options & option) != 0;
    }
  
  }//class

}//ns
