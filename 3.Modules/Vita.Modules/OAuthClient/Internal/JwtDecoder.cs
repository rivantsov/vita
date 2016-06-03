using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.OAuthClient.Internal {
  public static class JwtDecoder {
    // does not verify JWT; we should use it only over https, so verification is not needed
    public static OpenIdToken Decode(string token, IJsonDeserializer deserializer) {
      var parts = token.Split('.');
      if(parts.Length != 3) {
        throw new ArgumentException("Token must consist from 3 delimited by dot parts");
      }
      var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
      var tkn = deserializer.Deserialize<OpenIdToken>(payloadJson);
      return tkn;
    }

    // from JWT spec
    public static byte[] Base64UrlDecode(string input) {
      var output = input;
      output = output.Replace('-', '+'); // 62nd char of encoding
      output = output.Replace('_', '/'); // 63rd char of encoding
      switch(output.Length % 4) // Pad with trailing '='s
      {
        case 0: break; // No pad chars in this case
        case 2: output += "=="; break; // Two pad chars
        case 3: output += "="; break;  // One pad char
        default: throw new Exception("Illegal base64url string!");
      }
      var converted = Convert.FromBase64String(output); // Standard base64 decoder
      return converted;
    }


  }
}
