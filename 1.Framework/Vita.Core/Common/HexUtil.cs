using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {
  public static class HexUtil {

    // efficient bytes to string method
    // taken from here: http://stackoverflow.com/questions/311165/how-do-you-convert-byte-array-to-hexadecimal-string-and-vice-versa/632920#632920
    public static string ByteArrayToHex(byte[] barray) {
      char[] c = new char[barray.Length * 2];
      byte b;
      for (int i = 0; i < barray.Length; ++i) {
        b = ((byte)(barray[i] >> 4));
        c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
        b = ((byte)(barray[i] & 0xF));
        c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
      }
      return new string(c);
    }

    // Taken from here: http://stackoverflow.com/questions/321370/convert-hex-string-to-byte-array
    public static byte[] HexToByteArray(string hex) {
      Util.Check (hex.Length % 2 == 0, "The hex string cannot have an odd number of digits. String: '{0}'. ", hex);
      byte[] arr = new byte[hex.Length >> 1];
      for (int i = 0; i < hex.Length >> 1; ++i) {
        arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
      }
      return arr;
    }

    private static int GetHexVal(char hex) {
      int val = hex; // (int)hex;
      //For uppercase A-F letters:
      //return val - (val < 58 ? 48 : 55);
      //For lowercase a-f letters:
      //return val - (val < 58 ? 48 : 87);
      //Or the two combined, but a bit slower:
      return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
    }

  }
}
