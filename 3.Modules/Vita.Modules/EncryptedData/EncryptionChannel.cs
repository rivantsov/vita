using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;

namespace Vita.Modules.EncryptedData {

  internal class EncryptionChannel {
    public string Name;
    byte[] _cryptoKey;
    SymmetricAlgorithm _algorithm;

    public EncryptionChannel(string name, byte[] cryptoKey, SymmetricAlgorithm algorithm) {
      Name = name;
      _cryptoKey = cryptoKey;
      _algorithm = algorithm;
    }
    public byte[] Encrypt(byte[] data, string seed) {
      var iv = GetIV(seed);
      ICryptoTransform encr;
      lock(_algorithm) 
        encr = _algorithm.CreateEncryptor(_cryptoKey, iv);
      using(var outStream = new MemoryStream()) {
        using(var encStream = new CryptoStream(outStream, encr, CryptoStreamMode.Write)) {
          encStream.Write(data, 0, data.Length);
          encStream.FlushFinalBlock(); 
          encStream.Close();
          outStream.Close();
          var result = outStream.ToArray();
          return result;
        }
      }
    }

    public byte[] Decrypt(byte[] data, string seed = null) {
      var iv = GetIV(seed);
      ICryptoTransform decr;
      lock(_algorithm) 
        decr = _algorithm.CreateDecryptor(_cryptoKey, iv);
      using(var outStream = new MemoryStream()) {
        using(var encStream = new CryptoStream(outStream, decr, CryptoStreamMode.Write)) {
          encStream.Write(data, 0, data.Length);
          encStream.FlushFinalBlock(); //recommended
          encStream.Close();
          outStream.Close();
          return outStream.ToArray();
        }

      }
    }

    private byte[] GetIV(string seed) {
      seed = seed ?? EncryptedDataModule.DefaultSeed;
      var ivSize = _algorithm.BlockSize / 8;
      var iv = new byte[ivSize];
      var seedBytes = Encoding.UTF8.GetBytes(seed);
      for(int i = 0; i < ivSize; i++)
        iv[i] = seedBytes[i % seedBytes.Length];
      return iv;
    }


  } //class


}
