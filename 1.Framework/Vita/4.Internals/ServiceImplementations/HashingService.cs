using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Vita.Entities.Utilities;

namespace Vita.Entities.Services.Implementations {
  public class HashingService : IHashingService {

    public int ComputeHash(string value) {
      if(string.IsNullOrWhiteSpace(value))
        return 0;
      return Crc32.Compute(value.Trim());
    }

    public string ComputeMd5(string value) {
      // FIPS-compliant MD5
      var md5 = SHA256CryptoServiceProvider.Create();
      var bytes = Encoding.UTF8.GetBytes(value);
      var hash = md5.ComputeHash(bytes);
      var hashStr = HexUtil.ByteArrayToHex(hash);
      return hashStr; 
    }
  }
}
