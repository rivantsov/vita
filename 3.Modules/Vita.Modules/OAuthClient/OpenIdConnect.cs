using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities.Web;

namespace Vita.Modules.OAuthClient {

  public class OpenIdToken {
    [Node("sub")]
    public string Subject;
    [Node("iss")]
    public string Issuer;
    [Node("aud")]
    public string Audience;
    [Node("nonce")]
    public string Nonce;
    [Node("auth_time")]
    public long AuthTime;
    //  authentication context reference
    [Node("acr")]
    public string ContextRef;
    [Node("iat")]
    public long IssuedAt;
    [Node("exp")]
    public long ExpiresAt;
  }

  public enum OpenIdScopes {
    Email,
    Profile,
    Address,
    Phone
  }

  public static class OpenIdClaims {
    public static class Profile {
      public const string Scope = "profile";
      // name, family_name, given_name, middle_name, nickname, preferred_username, profile, 
      //  picture, website, gender, birthdate, zoneinfo, locale, updated_at
      public const string Name = "name";
      public const string FamilyName = "family_name";
      public const string GivenName = "given_name";
      //etc.
    }// class
    public static class Email {
      public const string Scope = "email";
      public const string EmailClaim = "email";
      public const string EmailVerified = "email_verified";
    }
    public static class Address {
      public const string Scope = "address";
      public const string AddressClaim = "address";
    }
    public static class Phone {
      public const string Scope = "phone";
      public const string PhoneNumber = "phone_number";
      public const string PhoneNumberVerified = "phone_number_verified";
    }
  }

  public static class OpenIdConnectUtil {
    // does not verify JWT; we should use it only over https, so verification is not needed
    public static string GetJwtPayload(string jwt) {
      var parts = jwt.Split('.');
      if(parts.Length != 3) {
        throw new ArgumentException("Token must consist from 3 delimited by dot parts");
      }
      var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
      return payloadJson;
    }

    // from JWT spec
    public static byte[] Base64UrlDecode(string input) {
      var output = input;
      output = output.Replace('-', '+'); // 62nd char of encoding
      output = output.Replace('_', '/'); // 63rd char of encoding
      switch(output.Length % 4) // Pad with trailing '='s
      {
        case 0:
          break; // No pad chars in this case
        case 2:
          output += "==";
          break; // Two pad chars
        case 3:
          output += "=";
          break;  // One pad char
        default:
          throw new Exception("Illegal base64url string!");
      }
      var converted = Convert.FromBase64String(output); // Standard base64 decoder
      return converted;
    }

    public static DateTime FromUnixTime(long unixTime) {
      var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      return epoch.AddSeconds(unixTime);
    }

  }

}
